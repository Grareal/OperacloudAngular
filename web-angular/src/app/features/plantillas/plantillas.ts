import { Component, ElementRef, ViewChild, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { PdfFieldCatalogItem, PdfTemplateDetail, PdfTemplateFieldInfo, PdfTemplateSummary } from '../../core/models';
import * as pdfjsLib from 'pdfjs-dist';

pdfjsLib.GlobalWorkerOptions.workerSrc = '/pdf.worker.min.mjs';

interface EditableField extends PdfTemplateFieldInfo {}

/** Diseñador de plantillas PDF (port de PdfTemplateDesigner.razor + template-designer.js). */
@Component({
  selector: 'app-plantillas',
  imports: [FormsModule],
  template: `
  <div class="d-flex justify-content-between align-items-start mb-3"><div><h3>Diseñador de plantillas PDF</h3><p class="text-muted">Archivos y mapeos locales; no modifica OPERA.</p></div></div>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  <div class="card mb-4"><div class="card-body"><h5>Nueva versión</h5><div class="row g-2 align-items-end">
    <div class="col-md-2"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="hotelId"></div>
    <div class="col-md-3"><label class="form-label">Tipo de documento</label><select class="form-select" [(ngModel)]="templateType"><option value="RegistrationCard">Tarjeta de registro</option><option value="Promotion">Promociones y beneficios</option><option value="PrivacyNotice">Aviso de privacidad</option><option value="Regulations">Reglamento</option><option value="Other">Otro</option></select></div>
    <div class="col-md-3"><label class="form-label">Nombre de plantilla</label><input class="form-control" [(ngModel)]="name"></div>
    <div class="col-md-3"><label class="form-label">PDF (máx. 20 MB)</label><input type="file" class="form-control" accept="application/pdf" (change)="onFile($event)"></div>
    <div class="col-md-1"><button type="button" class="btn btn-primary w-100" (click)="upload()" [disabled]="!file || working">Subir</button></div>
  </div></div></div>
  <div class="row g-3">
    <aside class="col-lg-3">
      <div class="card mb-3"><div class="card-body"><h5>Versiones</h5>
        <select class="form-select" [(ngModel)]="selectedId" (change)="loadDetail()">
          <option value="">Seleccione…</option>
          @for (t of templates; track t.id) { <option [value]="t.id">{{ t.hotelId }} · {{ typeLabel(t.templateType) }} · {{ t.name }} v{{ t.version }}</option> }
        </select>
      </div></div>
      @if (detail) {
        <div class="card"><div class="card-body"><h5>Campos de OPERA</h5><p class="small text-muted">Arrastre un campo hacia el PDF.</p>
          @for (f of visibleCatalog(); track f.key) {
            <div draggable="true" (dragstart)="onDragStart($event, f.key)" class="border rounded p-2 mb-2 bg-light" style="cursor:grab"><strong>{{ f.label }}</strong><div class="small text-muted">{{ f.type }}</div></div>
          }
          @if (detail.templateType === 'RegistrationCard') { <button type="button" class="btn btn-outline-primary w-100 mt-2" (click)="addOccupantPair()">+ Añadir acompañante</button> }
        </div></div>
      }
    </aside>
    <main class="col-lg-9">
      @if (detail) {
        <div class="card mb-2"><div class="card-body py-2"><div class="row g-2 align-items-end">
          <div class="col-md-3"><label>Página</label><div class="input-group"><button type="button" class="btn btn-outline-secondary" (click)="prevPage()">‹</button><span class="input-group-text flex-grow-1 justify-content-center">{{ page }} / {{ pageCount }}</span><button type="button" class="btn btn-outline-secondary" (click)="nextPage()">›</button></div></div>
          <div class="col-md-3"><label>Zoom</label><div class="input-group"><button type="button" class="btn btn-outline-secondary" (click)="zoomOut()">−</button><span class="input-group-text flex-grow-1 justify-content-center">{{ zoomPct() }}</span><button type="button" class="btn btn-outline-secondary" (click)="zoomIn()">+</button></div></div>
          <div class="col-md-2"><label>Room type inicia</label><input class="form-control" [(ngModel)]="roomPrefix"></div>
          <div class="col-md-2"><label>Idioma</label><input class="form-control" [(ngModel)]="language"></div>
          <div class="col-md-2"><button class="btn w-100" [class.btn-outline-danger]="detail.isPublished" [class.btn-dark]="!detail.isPublished" (click)="togglePublish()">{{ detail.isPublished ? 'Despublicar' : 'Publicar' }}</button></div>
          <div class="col-12 form-check ms-2"><input class="form-check-input" type="checkbox" [(ngModel)]="isDefault" id="dt"><label class="form-check-label" for="dt">Predeterminada del hotel</label></div>
        </div></div></div>
        <div class="d-flex flex-wrap gap-2 mb-2">
          <button type="button" class="btn btn-success" (click)="save()" [disabled]="working">Guardar mapeo</button>
          <button type="button" class="btn btn-outline-secondary" (click)="designMode = !designMode">{{ designMode ? 'Navegar PDF / usar zoom' : 'Editar mapeo' }}</button>
          <input class="form-control" style="max-width:220px" placeholder="Confirmación de prueba" [(ngModel)]="testConfirmation">
          <button type="button" class="btn btn-outline-primary" (click)="renderPreview()" [disabled]="working || !testConfirmation.trim()">Generar prueba local</button>
          <span class="text-muted align-self-center">{{ fields.length }} campos · Página {{ page }}</span>
        </div>
        <div class="alert alert-light py-2 small">Modo actual: <strong>{{ designMode ? 'edición de campos' : 'revisión' }}</strong>. El zoom escala la página y los campos en conjunto sin alterar sus coordenadas.</div>
        <div class="template-canvas-scroll">
          <div #surface id="templateDesignSurface" class="template-design-surface" [class.design-mode]="designMode" [style.width.px]="surfaceW" [style.height.px]="surfaceH"
               (dragover)="onDragOver($event)" (drop)="onDrop($event)">
            <canvas #canvas class="template-pdf-canvas"></canvas>
            @for (f of fields; track $index) {
              @if (f.pageNumber === page) {
                <div class="template-mapped-field" [class.signature]="f.fieldType === 'Signature'"
                     [style.left.%]="f.xPercent" [style.top.%]="f.yPercent" [style.width.%]="f.widthPercent" [style.height.%]="f.heightPercent"
                     [style.font-size.px]="f.fontSize * zoom" (pointerdown)="startMove($event, $index)" (click)="active = f">
                  <span>{{ f.label }}@if (f.occupantIndex) { #{{ f.occupantIndex }} }</span><button type="button" title="Quitar" (click)="remove($index); $event.stopPropagation()">×</button>
                </div>
              }
            }
          </div>
        </div>
        @if (active) {
          <div class="card mt-3"><div class="card-body"><strong>Editando: {{ active.label }}</strong>
            <div class="row g-2 mt-1">
              <div class="col"><label>X %</label><input type="number" step="0.1" class="form-control" [(ngModel)]="active.xPercent"></div>
              <div class="col"><label>Y %</label><input type="number" step="0.1" class="form-control" [(ngModel)]="active.yPercent"></div>
              <div class="col"><label>Ancho %</label><input type="number" step="0.1" class="form-control" [(ngModel)]="active.widthPercent"></div>
              <div class="col"><label>Alto %</label><input type="number" step="0.1" class="form-control" [(ngModel)]="active.heightPercent"></div>
              <div class="col"><label>Fuente</label><input type="number" step="0.5" class="form-control" [(ngModel)]="active.fontSize"></div>
            </div>
          </div></div>
        }
      } @else { <div class="alert alert-info">Suba o seleccione una plantilla para comenzar.</div> }
    </main>
  </div>`
})
export class PlantillasComponent implements OnInit {
  private api = inject(ApiService);
  @ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('surface') surfaceRef!: ElementRef<HTMLDivElement>;

  hotelId = 'VINV'; name = ''; templateType = 'RegistrationCard';
  file: File | null = null;
  templates: PdfTemplateSummary[] = [];
  catalog: PdfFieldCatalogItem[] = [];
  selectedId = '';
  detail: PdfTemplateDetail | null = null;
  fields: EditableField[] = [];
  active: EditableField | null = null;
  page = 1; pageCount = 1; zoom = 1.15;
  roomPrefix = ''; language = ''; isDefault = false;
  designMode = true; working = false;
  message = ''; isError = false;
  testConfirmation = '';
  surfaceW = 0; surfaceH = 0;
  private pdfDoc: any = null;
  private moving: number | null = null;

  ngOnInit() {
    this.api.getPdfTemplates().then(t => this.templates = t);
    this.api.getPdfFieldCatalog().then(c => this.catalog = c);
  }

  onFile(e: Event) { this.file = (e.target as HTMLInputElement).files?.[0] || null; }

  upload() {
    if (!this.file) return;
    this.working = true;
    this.api.uploadPdfTemplate(this.hotelId, this.name, this.templateType, this.file).then(
      r => this.api.getPdfTemplates().then(t => { this.templates = t; this.selectedId = r.id; this.loadDetail(); this.show('Plantilla cargada. Arrastre los campos sobre el documento.', false); this.working = false; }),
      e => { this.show(e?.error?.message || e.message, true); this.working = false; }
    );
  }

  loadDetail() {
    if (!this.selectedId) { this.detail = null; return; }
    this.api.getPdfTemplate(this.selectedId).then(async d => {
      this.detail = d; this.fields = [...d.fields]; this.active = null;
      this.isDefault = d.isDefault; this.roomPrefix = d.roomTypePrefix || ''; this.language = d.language || '';
      const bytes = await this.api.getPdfTemplateFile(d.id);
      const buf = await bytes.arrayBuffer();
      this.pdfDoc = await pdfjsLib.getDocument({ data: new Uint8Array(buf) }).promise;
      this.page = 1;
      setTimeout(() => this.renderPage(), 0);
    });
  }

  async renderPage() {
    if (!this.pdfDoc || !this.canvasRef) return;
    const pg = await this.pdfDoc.getPage(Math.max(1, Math.min(this.page, this.pdfDoc.numPages)));
    this.pageCount = this.pdfDoc.numPages;
    const vp = pg.getViewport({ scale: Math.max(.5, Math.min(3, this.zoom)) });
    const ratio = window.devicePixelRatio || 1;
    const cv = this.canvasRef.nativeElement;
    cv.width = Math.floor(vp.width * ratio); cv.height = Math.floor(vp.height * ratio);
    cv.style.width = vp.width + 'px'; cv.style.height = vp.height + 'px';
    this.surfaceW = vp.width; this.surfaceH = vp.height;
    await pg.render({ canvasContext: cv.getContext('2d')!, viewport: vp, transform: ratio === 1 ? undefined : [ratio, 0, 0, ratio, 0, 0] }).promise;
  }

  prevPage() { if (this.page > 1) { this.page--; this.renderPage(); } }
  nextPage() { if (this.page < this.pageCount) { this.page++; this.renderPage(); } }
  zoomIn() { this.zoom = Math.min(3, this.zoom + .15); this.renderPage(); }
  zoomOut() { this.zoom = Math.max(.5, this.zoom - .15); this.renderPage(); }
  zoomPct() { return Math.round(this.zoom * 100) + '%'; }

  visibleCatalog() {
    if (this.detail?.templateType === 'Promotion')
      return this.catalog.filter(x => ['GuestFullName','ConfirmationNumber','ArrivalDate','DepartureDate','RoomNumber','PromotionCampaigns','PromotionBenefits','UDFC02','UDFC16','UDFC20','PromotionSource','PromotionClassification'].includes(x.key));
    return this.catalog.filter(x => !x.key.startsWith('Promotion') && !x.key.startsWith('UDFC'));
  }

  onDragStart(e: DragEvent, key: string) { e.dataTransfer?.setData('text/template-field', key); }
  onDragOver(e: DragEvent) { e.preventDefault(); if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy'; }
  onDrop(e: DragEvent) {
    e.preventDefault();
    const key = e.dataTransfer?.getData('text/template-field');
    if (!key) return;
    const rect = this.surfaceRef.nativeElement.getBoundingClientRect();
    const x = Math.max(0, Math.min(98, (e.clientX - rect.left) * 100 / rect.width));
    const y = Math.max(0, Math.min(98, (e.clientY - rect.top) * 100 / rect.height));
    const item = this.catalog.find(c => c.key === key);
    if (!item || !this.detail) return;
    const occ = key.startsWith('Occupant') ? this.nextOccupant(key) : undefined;
    const f: EditableField = { id: '00000000-0000-0000-0000-000000000000', fieldKey: key, label: item.label, fieldType: item.type, pageNumber: this.page, xPercent: x, yPercent: y, widthPercent: item.type === 'Signature' ? 13 : 12, heightPercent: item.type === 'Signature' ? 4.5 : 2.4, fontSize: 7, occupantIndex: occ };
    this.fields.push(f); this.active = f;
  }

  startMove(e: PointerEvent, i: number) {
    if (!this.designMode) return;
    e.preventDefault();
    this.moving = i;
    const el = e.target as HTMLElement;
    el.setPointerCapture(e.pointerId);
    const surf = this.surfaceRef.nativeElement;
    const move = (ev: PointerEvent) => {
      if (this.moving == null) return;
      const rect = surf.getBoundingClientRect();
      this.fields[this.moving].xPercent = Math.max(0, Math.min(98, (ev.clientX - rect.left) * 100 / rect.width));
      this.fields[this.moving].yPercent = Math.max(0, Math.min(98, (ev.clientY - rect.top) * 100 / rect.height));
    };
    const up = (ev: PointerEvent) => {
      el.removeEventListener('pointermove', move as any);
      el.removeEventListener('pointerup', up as any);
      if (this.moving != null) this.active = this.fields[this.moving];
      this.moving = null;
    };
    el.addEventListener('pointermove', move as any);
    el.addEventListener('pointerup', up as any);
  }

  nextOccupant(key: string) { return Math.max(0, ...this.fields.filter(f => f.fieldKey === key).map(f => f.occupantIndex || 0)) + 1; }
  addOccupantPair() {
    const idx = Math.max(this.nextOccupant('OccupantName'), this.nextOccupant('OccupantSignature'));
    const nm = this.catalog.find(c => c.key === 'OccupantName')!;
    const sg = this.catalog.find(c => c.key === 'OccupantSignature')!;
    const y = Math.min(90, 8 + (idx - 1) * 7);
    this.fields.push({ id: '00000000-0000-0000-0000-000000000000', fieldKey: 'OccupantName', label: nm.label, fieldType: nm.type, pageNumber: this.page, xPercent: 8, yPercent: y, widthPercent: 12, heightPercent: 2.4, fontSize: 7, occupantIndex: idx });
    const s: EditableField = { id: '00000000-0000-0000-0000-000000000000', fieldKey: 'OccupantSignature', label: sg.label, fieldType: sg.type, pageNumber: this.page, xPercent: 55, yPercent: y, widthPercent: 13, heightPercent: 4.5, fontSize: 7, occupantIndex: idx };
    this.fields.push(s); this.active = s;
    this.show(`Acompañante #${idx} agregado. Mueva nombre y firma a sus espacios.`, false);
  }
  remove(i: number) { this.fields.splice(i, 1); }

  save() {
    if (!this.detail) return;
    this.working = true;
    const expected = this.fields.length;
    this.api.savePdfTemplateFields(this.detail.id, this.fields).then(
      () => this.api.getPdfTemplate(this.detail!.id).then(d => {
        this.fields = [...d.fields]; this.active = null;
        if (this.fields.length !== expected) throw new Error(`Se enviaron ${expected} campos pero la base devolvió ${this.fields.length}.`);
        this.show(`Mapeo guardado correctamente: ${this.fields.length} campos persistidos.`, false);
        this.working = false;
      }, e => { this.show('No se guardó el mapeo: ' + (e?.error?.message || e.message), true); this.working = false; }),
      e => { this.show('No se guardó el mapeo: ' + (e?.error?.message || e.message), true); this.working = false; }
    );
  }

  renderPreview() {
    if (!this.detail) return;
    this.working = true;
    this.saveSilent().then(() => this.api.renderPdfTemplate(this.detail!.id, this.testConfirmation.trim()).then(
      b => { this.api.downloadBlob(b, `${this.detail!.name}-${this.testConfirmation}-PRUEBA.pdf`); this.show('Prueba local generada. No se escribió información en OPERA.', false); this.working = false; },
      e => { this.show(e?.error?.message || e.message, true); this.working = false; }
    ));
  }
  private saveSilent() {
    if (!this.detail) return Promise.resolve();
    return this.api.savePdfTemplateFields(this.detail.id, this.fields).then(() => this.api.getPdfTemplate(this.detail!.id)).then(d => { this.fields = [...d.fields]; });
  }

  togglePublish() {
    if (!this.detail) return;
    const d = this.detail;
    const op = d.isPublished
      ? this.api.unpublishPdfTemplate(d.id)
      : this.saveSilent().then(() => this.api.publishPdfTemplate(d.id, this.isDefault, this.roomPrefix, this.language));
    Promise.resolve(op).then(
      () => this.api.getPdfTemplates().then(t => { this.templates = t; this.loadDetail(); this.show(this.detail!.isPublished ? 'Plantilla publicada.' : 'Plantilla despublicada.', false); }),
      e => this.show(e?.error?.message || e.message, true)
    );
  }

  typeLabel(t: string) { return { Promotion: 'Promociones', PrivacyNotice: 'Aviso', Regulations: 'Reglamento', Other: 'Otro' }[t] || 'Registration Card'; }
  show(t: string, err: boolean) { this.message = t; this.isError = err; }
}
