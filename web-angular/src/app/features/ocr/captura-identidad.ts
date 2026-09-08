import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { OcrParseResult } from '../../core/models';

@Component({
  selector: 'app-captura-identidad',
  imports: [FormsModule],
  template: `
  <div class="d-flex justify-content-between align-items-start mb-3">
    <div><h3>Captura guiada de identidad <span class="badge bg-warning text-dark">POC local</span></h3>
    <p class="text-muted mb-0">Tesseract on-prem. No consulta ni escribe OPERA. Revisión humana obligatoria.</p></div>
  </div>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  <div class="row g-3">
    <div class="col-lg-4">
      <div class="card mb-3"><div class="card-body">
        <h5>1. Guía de captura</h5>
        <ul class="small mb-2">
          <li>Buena luz difusa, <strong>sin flash directo</strong> (evita brillo en la mica).</li>
          <li>INE completa dentro del marco, sin recortar bordes ni vigencia.</li>
          <li>Fondo oscuro y plano, credencial sin mica protectora si es posible.</li>
          <li>Enfocar y mantener el teléfono quieto 1 segundo.</li>
        </ul>
        <div class="form-check"><input class="form-check-input" type="checkbox" [(ngModel)]="guiaOk" id="guiaOk"><label class="form-check-label" for="guiaOk">Verifiqué luz, encuadre y enfoque</label></div>
      </div></div>
      <div class="card"><div class="card-body">
        <h5>2. Reserva</h5>
        <div class="row g-2">
          <div class="col-6"><label class="form-label">Confirmación</label><input class="form-control" [(ngModel)]="confirmation"></div>
          <div class="col-3"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="hotelId"></div>
          <div class="col-3"><label class="form-label">Hab.</label><input class="form-control" [(ngModel)]="room"></div>
          <div class="col-12"><label class="form-label">Documento</label><select class="form-select" [(ngModel)]="docType"><option value="Auto">Auto</option><option value="INE">INE</option><option value="Pasaporte">Pasaporte</option></select></div>
        </div>
      </div></div>
    </div>
    <div class="col-lg-8">
      <div class="row g-3">
        <div class="col-md-6"><div class="card"><div class="card-body">
          <h5>Frente</h5>
          <input type="file" class="form-control mb-2" accept="image/*" (change)="load($event, true)">
          @if (frontUrl) { <div class="id-frame"><img [src]="frontUrl" alt="Frente"><span class="corner tl"></span><span class="corner tr"></span><span class="corner bl"></span><span class="corner br"></span></div> }
        </div></div></div>
        <div class="col-md-6"><div class="card"><div class="card-body">
          <h5>Reverso / CURP</h5>
          <input type="file" class="form-control mb-2" accept="image/*" (change)="load($event, false)">
          @if (backUrl) { <div class="id-frame"><img [src]="backUrl" alt="Reverso"><span class="corner tl"></span><span class="corner tr"></span><span class="corner bl"></span><span class="corner br"></span></div> }
        </div></div></div>
      </div>
      <div class="d-flex gap-2 my-3">
        <button class="btn btn-primary" (click)="analyze()" [disabled]="working || !front || !guiaOk">Analizar OCR</button>
        <button class="btn btn-success" (click)="generate()" [disabled]="working || !front || !guiaOk || !confirmation.trim() || !parse || !reviewOk || !retentionOk">Generar PDF local</button>
        @if (pdfBlob) { <button class="btn btn-outline-secondary" (click)="download()">Descargar PDF</button> }
        @if (working) { <span class="text-muted align-self-center">Procesando…</span> }
      </div>
      @if (parse) {
        <div class="card"><div class="card-body">
          <h5>3. Revisión humana <span class="badge bg-info">frente {{ pct(parse.frontConfidence) }}</span>@if (parse.backConfidence != null) { <span class="badge bg-info">reverso {{ pct(parse.backConfidence) }}</span> }</h5>
          @if (parse.warnings.length) { <div class="alert alert-warning py-2">{{ parse.warnings.join(' | ') }}</div> }
          <div class="row g-2">
            <div class="col-md-6"><label class="form-label">Nombre</label><input class="form-control" [(ngModel)]="parse.fields.fullName"></div>
            <div class="col-md-3"><label class="form-label">CURP</label><input class="form-control font-monospace" [(ngModel)]="parse.fields.curp"></div>
            <div class="col-md-3"><label class="form-label">Clave elector</label><input class="form-control font-monospace" [(ngModel)]="parse.fields.claveElector"></div>
            <div class="col-md-3"><label class="form-label">Vigencia</label><input class="form-control" [(ngModel)]="parse.fields.vigencia"></div>
            <div class="col-md-3"><label class="form-label">Pasaporte</label><input class="form-control font-monospace" [(ngModel)]="parse.fields.passportNumber"></div>
            <div class="col-md-6"><label class="form-label">Tipo detectado</label><input class="form-control" [(ngModel)]="parse.fields.docType" readonly></div>
          </div>
          <div class="small text-muted mt-2">Confianza por campo: nombre {{ fieldPct('fullName') }} · CURP {{ fieldPct('curp') }} · elector {{ fieldPct('claveElector') }} · pasaporte {{ fieldPct('passportNumber') }}</div>
          <div class="form-check mt-3"><input class="form-check-input" type="checkbox" [(ngModel)]="reviewOk" id="reviewOk"><label class="form-check-label" for="reviewOk">Comparé y confirmé cada dato contra el documento original</label></div>
          <div class="form-check"><input class="form-check-input" type="checkbox" [(ngModel)]="retentionOk" id="retentionOk"><label class="form-check-label" for="retentionOk">Confirmo que existe finalidad y autorización para conservar esta evidencia</label></div>
          <details class="mt-2"><summary class="small text-muted">Texto OCR crudo</summary><pre class="small bg-light p-2">{{ parse.frontText }}</pre></details>
        </div></div>
      }
      @if (pdfName) { <div class="alert alert-success mt-3">PDF guardado local: <strong>{{ pdfName }}</strong> · SHA-256 <code>{{ pdfHash }}</code> · Sin envío a OPERA.</div> }
    </div>
  </div>`
})
export class CapturaIdentidadComponent {
  private api = inject(ApiService);
  confirmation = ''; hotelId = 'VINV'; room = ''; docType = 'Auto';
  guiaOk = false; reviewOk = false; retentionOk = false; working = false; isError = false; message = '';
  front: File | null = null; back: File | null = null;
  frontUrl: string | null = null; backUrl: string | null = null;
  parse: OcrParseResult | null = null;
  pdfBlob: Blob | null = null; pdfName = ''; pdfHash = '';

  load(e: Event, front: boolean) {
    const f = (e.target as HTMLInputElement).files?.[0] || null;
    if (!f) return;
    if (!['image/jpeg', 'image/png'].includes(f.type) || f.size > 10_000_000) {
      this.show('Use una imagen JPEG o PNG de máximo 10 MB.', true); return;
    }
    if (front) { this.front = f; this.frontUrl = URL.createObjectURL(f); }
    else { this.back = f; this.backUrl = URL.createObjectURL(f); }
    this.parse = null; this.reviewOk = false; this.retentionOk = false; this.pdfBlob = null; this.pdfName = '';
  }
  analyze() {
    if (!this.front) return;
    this.working = true; this.message = '';
    this.api.parseIdentity(this.front, this.back, this.docType).then(
      r => { this.parse = r; this.show(r.warnings.length ? 'OCR listo con advertencias: revise los campos.' : 'OCR listo. Verifique los campos antes de generar el PDF.', false); this.working = false; },
      e => { this.show('No se analizó: ' + (e?.error?.message || e.message), true); this.working = false; }
    );
  }
  generate() {
    if (!this.front || !this.parse || !this.reviewOk || !this.retentionOk) return;
    this.working = true; this.message = '';
    this.api.createIdentityPdf(this.confirmation.trim(), this.hotelId.trim().toUpperCase(), this.room.trim(), this.front, this.back, this.docType, this.parse.fields).then(
      resp => {
        this.pdfBlob = resp.body;
        this.pdfHash = resp.headers.get('X-Document-Hash') || '';
        const cd = resp.headers.get('Content-Disposition') || '';
        const m = /filename\*?=(?:UTF-8'')?("?)([^";]+)\1/.exec(cd);
        this.pdfName = m ? decodeURIComponent(m[2]) : `ID${this.confirmation.trim()}-EVIDENCIA.pdf`;
        this.show('PDF local generado y guardado. No se envió a OPERA.', false);
        this.working = false;
      },
      e => { this.show('No se generó: ' + (e?.error?.message || e.message), true); this.working = false; }
    );
  }
  download() { if (this.pdfBlob) this.api.downloadBlob(this.pdfBlob, this.pdfName); }
  pct(c: number) { return c <= 1 ? Math.round(c * 100) + '%' : c + '%'; }
  fieldPct(key: string) { return this.parse ? this.pct(this.parse.fieldConfidences[key] || 0) : '0%'; }
  show(t: string, err: boolean) { this.message = t; this.isError = err; }
}
