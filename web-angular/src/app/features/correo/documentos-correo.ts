import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { CommunicationDocumentInfo, GuestEmailDeliveryInfo } from '../../core/models';

@Component({
  selector: 'app-documentos-correo',
  imports: [FormsModule, RouterLink],
  template: `
  <header class="mb-4"><div class="page-kicker">Administración</div><h1 class="page-heading">Paquete documental por correo</h1><p class="page-subtitle">Configure avisos, reglamentos, promociones o cualquier archivo que acompañará a la tarjeta firmada.</p></header>
  <div class="alert alert-info d-flex flex-wrap justify-content-between align-items-center gap-2"><span><strong>¿Promoción con datos de OPERA?</strong> Use el diseñador para mapear UDFC16, UDFC02 y generar una versión distinta por reserva.</span><a class="btn btn-sm btn-primary" routerLink="/plantillas-pdf">Abrir diseñador dinámico</a></div>
  <section class="panel mb-4">
    <h2 class="section-title">Nueva versión</h2>
    <div class="row g-3">
      <div class="col-md-2"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="edit.hotelId"></div>
      <div class="col-md-3"><label class="form-label">Tipo</label><select class="form-select" [(ngModel)]="edit.type"><option value="PrivacyNotice">Aviso de privacidad</option><option value="Regulations">Reglamento</option><option value="Promotion">Promoción</option><option value="Other">Otro</option></select></div>
      <div class="col-md-4"><label class="form-label">Nombre</label><input class="form-control" [(ngModel)]="edit.name" placeholder="Ej. Reglamento general"></div>
      <div class="col-md-2"><label class="form-label">Idioma</label><select class="form-select" [(ngModel)]="edit.language"><option>EN</option><option>ES</option><option>ALL</option></select></div>
      <div class="col-md-1"><label class="form-label">Orden</label><input class="form-control" type="number" [(ngModel)]="edit.sortOrder"></div>
      <div class="col-md-6"><label class="form-label">Subir archivo (máx. 20 MB)</label><input type="file" class="form-control" (change)="onFile($event)"></div>
      <div class="col-md-6"><label class="form-label">O usar URL HTTPS</label><input class="form-control" [(ngModel)]="remoteUrl" placeholder="https://.../documento.pdf"></div>
      <div class="col-md-3"><label class="form-label">Nombre de archivo remoto</label><input class="form-control" [(ngModel)]="remoteFileName" placeholder="aviso.pdf"></div>
      <div class="col-md-3"><label class="form-label">Vigente desde</label><input class="form-control" type="date" [(ngModel)]="from"></div>
      <div class="col-md-3"><label class="form-label">Vigente hasta</label><input class="form-control" type="date" [(ngModel)]="to"></div>
      <div class="col-md-3 d-flex flex-column justify-content-end gap-2">
        <label class="form-check"><input class="form-check-input" type="checkbox" [(ngModel)]="edit.isRequired"> <span class="form-check-label">Documento obligatorio</span></label>
        <label class="form-check"><input class="form-check-input" type="checkbox" [(ngModel)]="edit.requiresMarketingConsent"> <span class="form-check-label">Requiere consentimiento promocional</span></label>
      </div>
      <div class="col-12"><button class="btn btn-primary" type="button" (click)="create()" [disabled]="busy">{{ busy ? 'Guardando…' : 'Crear versión' }}</button></div>
    </div>
    @if (message) { <div class="alert mt-3 mb-0" [class.alert-danger]="isError" [class.alert-info]="!isError">{{ message }}</div> }
  </section>
  <section class="panel">
    <div class="d-flex justify-content-between align-items-center mb-3"><h2 class="section-title mb-0">Versiones configuradas</h2><button class="btn btn-outline-secondary btn-sm" (click)="load()">Actualizar</button></div>
    <div class="table-responsive"><table class="table align-middle"><thead><tr><th>Documento</th><th>Origen</th><th>Idioma</th><th>Vigencia</th><th>Reglas</th><th>Estado</th><th></th></tr></thead><tbody>
      @for (it of docs; track it.id) {
        <tr><td><strong>{{ it.name }}</strong><br><small>{{ label(it.type) }} · v{{ it.version }} · {{ it.fileName }}</small></td><td>{{ it.source === 'RemoteUrl' ? 'URL oficial' : 'Archivo local' }}</td><td>{{ it.language }}</td><td><small>{{ dt(it.effectiveFromUtc) }} — {{ dt(it.effectiveToUtc) }}</small></td><td><small>{{ it.isRequired ? 'Obligatorio' : 'Opcional' }}<br>{{ it.requiresMarketingConsent ? 'Con consentimiento' : 'Sin consentimiento comercial' }}</small></td><td><span class="badge" [class.text-bg-success]="it.isPublished" [class.text-bg-secondary]="!it.isPublished">{{ it.isPublished ? 'Publicado' : 'Borrador' }}</span></td><td><button class="btn btn-sm" [class.btn-outline-secondary]="it.isPublished" [class.btn-primary]="!it.isPublished" (click)="toggle(it)">{{ it.isPublished ? 'Despublicar' : 'Publicar' }}</button></td></tr>
      }
    </tbody></table></div>
  </section>
  <section class="panel mt-4">
    <h2 class="section-title mb-3">Historial de envíos</h2>
    <div class="table-responsive"><table class="table align-middle"><thead><tr><th>Reserva</th><th>Destinatario</th><th>Paquete</th><th>Estado</th><th>Fecha</th><th></th></tr></thead><tbody>
      @for (dv of deliveries; track dv.id) {
        <tr><td><strong>{{ dv.confirmationNumber }}</strong><br><small>{{ dv.guestName }}</small></td><td>{{ dv.recipientEmail }}</td><td>{{ dv.itemCount }} archivo(s)<br><small>{{ dv.marketingConsent ? 'Promociones autorizadas' : 'Sin promociones' }}</small></td><td><span class="badge" [class.text-bg-success]="dv.status === 'Sent'" [class.text-bg-danger]="dv.status === 'Failed'" [class.text-bg-warning]="dv.status === 'Retry'" [class.text-bg-secondary]="dv.status !== 'Sent' && dv.status !== 'Failed' && dv.status !== 'Retry'">{{ dv.status }}</span>@if (dv.lastError) { <div class="text-danger small mt-1">{{ dv.lastError }}</div> }</td><td>{{ dtt(dv.createdAtUtc) }}</td><td>@if (dv.status !== 'Sending') { <button class="btn btn-sm btn-outline-primary" (click)="retry(dv.id)">{{ dv.status === 'Sent' ? 'Reenviar' : 'Reintentar' }}</button> }</td></tr>
      }
    </tbody></table></div>
  </section>`
})
export class DocumentosCorreoComponent implements OnInit {
  private api = inject(ApiService);
  edit: any = { hotelId: 'VINV', name: 'Aviso de privacidad', type: 'PrivacyNotice', language: 'EN', sortOrder: 10, isRequired: true, requiresMarketingConsent: false };
  docs: CommunicationDocumentInfo[] = [];
  deliveries: GuestEmailDeliveryInfo[] = [];
  file: File | null = null;
  remoteUrl = ''; remoteFileName = ''; from = ''; to = '';
  message = ''; isError = false; busy = false;

  ngOnInit() { this.load(); }
  load() {
    this.api.getCommunicationDocuments(this.edit.hotelId).then(d => this.docs = d);
    this.api.getGuestEmailDeliveries(this.edit.hotelId).then(d => this.deliveries = d);
  }
  onFile(e: Event) { this.file = (e.target as HTMLInputElement).files?.[0] || null; }
  create() {
    this.busy = true; this.isError = false; this.message = '';
    const payload = { ...this.edit, effectiveFromUtc: this.from ? new Date(this.from).toISOString() : null, effectiveToUtc: this.to ? new Date(this.to + 'T23:59:59').toISOString() : null };
    const done = () => { this.message = 'Versión creada como borrador. Publíquela cuando esté revisada.'; this.file = null; this.remoteUrl = ''; this.load(); this.busy = false; };
    const fail = (e: any) => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; };
    if (!this.edit.name?.trim()) { fail({ message: 'Capture el nombre del documento.' }); return; }
    if (this.file) this.api.uploadCommunicationDocument(payload, this.file).then(done, fail);
    else if (this.remoteUrl.trim()) this.api.addRemoteCommunicationDocument(payload, this.remoteUrl.trim(), this.remoteFileName.trim() || 'documento.pdf').then(done, fail);
    else fail({ message: 'Seleccione un archivo o indique una URL HTTPS.' });
  }
  toggle(it: CommunicationDocumentInfo) {
    const { hotelId, type, name, language, isRequired, requiresMarketingConsent, sortOrder, effectiveFromUtc, effectiveToUtc } = it;
    this.api.saveCommunicationDocumentPublication(it.id, { hotelId, type, name, language, isRequired, requiresMarketingConsent, sortOrder, effectiveFromUtc, effectiveToUtc }, !it.isPublished).then(() => this.load());
  }
  retry(id: string) { this.api.retryGuestEmailDelivery(id).then(() => this.load()); }
  label(t: string) { return { PrivacyNotice: 'Aviso de privacidad', Regulations: 'Reglamento', Promotion: 'Promoción' }[t] || 'Otro'; }
  dt(v?: string) { return v ? new Date(v).toLocaleDateString('es-MX') : 'Sin límite'; }
  dtt(v: string) { return new Date(v).toLocaleString('es-MX', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
}
