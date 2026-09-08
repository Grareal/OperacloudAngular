import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ReservationDocumentPackage } from '../../core/models';

@Component({
  selector: 'app-expediente',
  imports: [FormsModule],
  template: `
  <header class="mb-4">
    <div class="page-kicker">Visualización y auditoría</div>
    <h1 class="page-heading">Expediente documental de la reserva</h1>
    <p class="page-subtitle">Consulte tarjetas firmadas, evidencias, promociones y los paquetes preparados para correo.</p>
  </header>
  <section class="panel search-panel">
    <label class="form-label" for="confirmation">Número de reserva</label>
    <div class="input-group input-group-lg">
      <span class="input-group-text">⌕</span>
      <input id="confirmation" class="form-control" [(ngModel)]="confirmation" (keydown.enter)="search()" placeholder="Ej. 3507296" autocomplete="off">
      <button class="btn btn-primary px-4" [disabled]="busy || !confirmation.trim() || accessReason.trim().length < 5" (click)="search()">{{ busy ? 'Consultando…' : 'Consultar expediente' }}</button>
    </div>
    <label class="form-label mt-3" for="accessReason">Motivo de consulta (se registrará en auditoría)</label>
    <input id="accessReason" class="form-control" [(ngModel)]="accessReason" maxlength="500" placeholder="Ej. atención al huésped en recepción" autocomplete="off">
    <div class="form-text">Consulta únicamente información almacenada en FirmaOperaCloud; no modifica OPERA.</div>
  </section>
  @if (error) { <div class="alert alert-danger mt-3">{{ error }}</div> }
  @if (message) { <div class="alert alert-success mt-3">{{ message }}</div> }
  @if (result && !result.found) {
    <div class="empty-state mt-4"><div class="empty-icon">▤</div><h2>Sin documentos locales</h2><p>No existen firmas, PDFs ni paquetes de correo asociados a la reserva <strong>{{ result.confirmationNumber }}</strong>.</p></div>
  } @else if (result) {
    <div class="summary-strip mt-4">
      <div><small>Reserva</small><strong>{{ result.confirmationNumber }}</strong></div>
      <div><small>Huésped</small><strong>{{ result.guestName || 'No registrado' }}</strong></div>
      <div><small>Habitación</small><strong>{{ result.roomNumber || '—' }}</strong></div>
      <div><small>Documentos</small><strong>{{ result.documents.length }}</strong></div>
      <div><small>Firmas</small><strong>{{ result.signatures.length }}</strong></div>
      <div><small>Paquetes</small><strong>{{ result.deliveries.length }}</strong></div>
    </div>
    @if (result.files.length) {
      <section class="panel mt-4">
        <div class="section-heading"><div><div class="page-kicker">Contenedor sellable</div><h2>Versiones del expediente</h2></div></div>
        @for (f of result.files; track f.id) {
          <article class="document-row">
            <div class="file-symbol">EXP</div>
            <div class="file-data"><strong>Expediente v{{ f.version }} · {{ f.status }}</strong><span>{{ f.documentCount }} documento(s) · {{ dt(f.createdAtUtc) }}</span><small>SHA-256 manifiesto: {{ f.manifestHash || 'pendiente' }}</small></div>
            @if (isAdmin && f.status === 'Open') { <button class="btn btn-sm btn-warning" (click)="seal(f.id)">Sellar</button> }
          </article>
        }
      </section>
    }
    <div class="row g-4 mt-1">
      <div class="col-xl-7">
        <section class="panel h-100">
          <div class="section-heading"><div><div class="page-kicker">Archivos definitivos</div><h2>Tarjetas de registro</h2></div><span class="count-pill">{{ result.documents.length }}</span></div>
          @if (!result.documents.length) { <p class="text-muted mb-0">Todavía no se ha almacenado una tarjeta definitiva.</p> }
          @for (d of result.documents; track d.id) {
            <article class="document-row">
              <div class="file-symbol">PDF</div>
              <div class="file-data"><strong>{{ d.fileName }}</strong><span>Versión {{ d.version }} · {{ dt(d.createdAtUtc) }} · {{ d.signatureCount }} firma(s)</span><small>Estado: {{ d.status }}@if (d.attachmentId) { · Attachment OPERA: {{ d.attachmentId }} }</small></div>
              <button class="btn btn-sm btn-outline-primary" (click)="downloadDoc(d.id, d.fileName)">Descargar</button>
            </article>
          }
        </section>
      </div>
      <div class="col-xl-5">
        <section class="panel h-100">
          <div class="section-heading"><div><div class="page-kicker">Evidencia biométrica</div><h2>Firmantes</h2></div><span class="count-pill">{{ result.signatures.length }}</span></div>
          @if (!result.signatures.length) { <p class="text-muted mb-0">No hay firmas vinculadas.</p> }
          @for (s of result.signatures; track s.id) {
            <article class="signature-row">
              <div class="signature-image">@if (images[s.id]) { <img [src]="images[s.id]" [alt]="'Firma de ' + s.signerName"> } @else { <span>Firma</span> }</div>
              <div><strong>{{ s.signerName }}</strong><span>{{ role(s.signerRole) }} · {{ dt(s.signedAtUtc) }}</span><small>ID: {{ s.operaProfileId || s.signerKey }}</small></div>
            </article>
          }
        </section>
      </div>
    </div>
    <section class="panel mt-4">
      <div class="section-heading"><div><div class="page-kicker">Comunicaciones</div><h2>Paquetes documentales generados</h2></div><span class="count-pill">{{ result.deliveries.length }}</span></div>
      @if (!result.deliveries.length) { <p class="text-muted mb-0">No se ha preparado ningún paquete de correo para esta reserva.</p> }
      @for (dv of result.deliveries; track dv.id; let first = $first) {
        <details class="delivery-card" [open]="first">
          <summary><span><strong>{{ dt(dv.createdAtUtc) }}</strong><small>{{ dv.recipientEmail }} · {{ dv.items.length }} archivo(s)</small></span><span class="status-chip">{{ status(dv.status) }}</span></summary>
          <div class="delivery-items">
            @if (isAdmin && dv.status !== 'Sending') {
              <div class="mb-3"><button class="btn btn-sm btn-outline-primary" [disabled]="resending" (click)="resend(dv.id, dv.recipientEmail, dv.status)">{{ resending ? 'Encolando...' : dv.status === 'Sent' ? 'Reenviar correo' : 'Reintentar envío' }}</button></div>
            }
            @for (it of dv.items; track it.id) {
              <div class="attachment-row"><span class="attachment-icon">↧</span><div><strong>{{ it.name }}</strong><small>{{ typeName(it.documentType) }} · v{{ it.version }} · {{ it.fileName }}</small></div><button class="btn btn-sm btn-link" (click)="downloadItem(it.id, it.fileName)">Abrir archivo</button></div>
            }
            @if (dv.lastError) { <div class="alert alert-warning mb-0 mt-2"><strong>Último intento:</strong> {{ dv.lastError }}</div> }
          </div>
        </details>
      }
    </section>
    <section class="panel mt-4">
      <div class="section-heading"><div><div class="page-kicker">Prueba sin envío</div><h2>Vista previa del correo</h2></div><span class="safe-badge">No envía correo</span></div>
      <div class="email-shell">
        <div class="email-toolbar"><span></span><span></span><span></span></div>
        <div class="email-header">
          <div><small>De</small><strong>{{ result.emailPreview.fromName }} &lt;{{ result.emailPreview.fromAddress }}&gt;</strong></div>
          <div><small>Para</small><strong>{{ result.emailPreview.recipient || 'correo-del-huesped@ejemplo.com' }}</strong></div>
          <div><small>Asunto</small><strong>{{ result.emailPreview.subject }}</strong></div>
        </div>
        <div class="email-body" [innerHTML]="result.emailPreview.bodyHtml"></div>
        <div class="email-attachments"><small>ARCHIVOS ADJUNTOS ({{ result.emailPreview.attachments.length }})</small>
          @for (a of result.emailPreview.attachments; track a.id) { <span>▤ {{ a.fileName }}</span> }
        </div>
      </div>
    </section>
  }`
})
export class ExpedienteComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  auth = inject(AuthService);
  confirmation = ''; accessReason = ''; busy = false; resending = false; error = ''; message = '';
  result: ReservationDocumentPackage | null = null;
  images: Record<string, string> = {};

  ngOnInit() {
    const q = this.route.snapshot.queryParams['reserva'];
    if (q) { this.confirmation = q; this.search(); }
  }
  get isAdmin() { return this.auth.role === 'Admin'; }

  search() {
    if (this.accessReason.trim().length < 5) { this.error = 'Indique un motivo de consulta de al menos 5 caracteres.'; return; }
    this.busy = true; this.error = ''; this.message = ''; this.images = {};
    this.api.getReservationPackage(this.confirmation.trim(), this.accessReason.trim()).then(async r => {
      this.result = r;
      for (const s of r.signatures) {
        try {
          const b = await this.api.getSignatureImage(s.id, this.accessReason.trim());
          const buf = await b.arrayBuffer();
          let bin = ''; const bytes = new Uint8Array(buf);
          for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
          this.images[s.id] = 'data:image/png;base64,' + btoa(bin);
        } catch { /* sin imagen */ }
      }
      this.busy = false;
    }, e => { this.error = e?.error?.message || e.message; this.result = null; this.busy = false; });
  }
  downloadDoc(id: string, name: string) { this.api.downloadLocalDocument(id, this.accessReason.trim()).then(b => this.api.downloadBlob(b, name)); }
  downloadItem(id: string, name: string) { this.api.downloadEmailItem(id, this.accessReason.trim()).then(b => this.api.downloadBlob(b, name)); }
  seal(id: string) {
    const reason = window.prompt('Motivo del sellado (mínimo 10 caracteres):')?.trim() || '';
    if (reason.length < 10) return;
    this.api.sealReservationFile(id, reason).then(
      () => { this.message = 'Expediente sellado.'; this.search(); },
      e => this.error = e?.error?.message || e.message
    );
  }
  resend(id: string, email: string, st: string) {
    this.resending = true;
    this.api.retryGuestEmailDelivery(id).then(
      () => { this.message = `El paquete para ${email} quedó en cola para envío SMTP.`; this.search(); this.resending = false; },
      e => { this.error = e?.error?.message || e.message; this.resending = false; }
    );
  }
  dt(v: string) { return new Date(v).toLocaleString('es-MX', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
  role(r: string) { return r === 'PrimaryGuest' ? 'Huésped principal' : 'Acompañante'; }
  status(s: string) { return { Sent: 'Enviado', Pending: 'Pendiente', Retry: 'Reintento', Failed: 'Fallido', Sending: 'Enviando' }[s] || s; }
  typeName(t: string) { return { RegistrationCard: 'Tarjeta firmada', PrivacyNotice: 'Aviso de privacidad', Regulations: 'Reglamento', Promotion: 'Promoción' }[t] || 'Documento'; }
}
