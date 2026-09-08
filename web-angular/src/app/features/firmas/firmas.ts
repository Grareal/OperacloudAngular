import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { StoredSignatureInfo } from '../../core/models';

@Component({
  selector: 'app-firmas',
  imports: [FormsModule],
  template: `
  <section class="waiter-search-stage">
    <div class="waiter-title"><div class="page-kicker">Validación operativa</div><h1>Localice la firma del huésped</h1><p>Consulte de forma rápida antes de validar una cuenta o consumo.</p></div>
    <div class="waiter-search-card">
      <div class="waiter-search-icon">⌕</div>
      <div class="waiter-search-copy"><label for="waiter-search">Reserva, habitación o nombre</label><span>Puede escribir el dato completo o parte del nombre del huésped.</span></div>
      <div class="waiter-search-control">
        <input id="waiter-search" [(ngModel)]="term" (keydown.enter)="search()" placeholder="Ej. 72101, 589474005 o Bautista" autofocus>
        <button class="btn btn-primary" (click)="search()" [disabled]="loading || term.trim().length < 2">{{ loading ? 'Buscando…' : 'Buscar firma' }}</button>
      </div>
      <div class="waiter-search-hints"><span><b>1</b> Capture un dato</span><span><b>2</b> Seleccione la firma</span><span><b>3</b> Compare visualmente</span></div>
    </div>
  </section>
  @if (error) { <div class="alert alert-danger">{{ error }}</div> }
  @if (compare.length) {
    <section class="card waiter-compare mb-4 border-primary"><div class="card-body">
      <div class="d-flex justify-content-between"><h4>Comparación lado a lado</h4><button class="btn btn-sm btn-outline-secondary" (click)="compare = []">Limpiar</button></div>
      <div class="row g-3">
        @for (c of compare; track c.id) {
          <div class="col-md-6"><h5>{{ c.signerName }}</h5><div class="small text-muted mb-2">{{ c.confirmationNumber }} · Hab. {{ c.roomNumber }} · {{ dt(c.signedAtUtc) }}</div>
          @if (images[c.id]) { <div class="border rounded p-3 text-center bg-white"><img [src]="images[c.id]" alt="Firma para comparar" style="width:100%;height:180px;object-fit:contain"></div> }</div>
        }
      </div>
      <p class="small text-muted mt-3 mb-0">La comparación es visual y debe evaluarla una persona autorizada.</p>
    </div></section>
  }
  @if (loading) { <div class="spinner-border text-primary"></div> }
  @else if (searched && !rows.length) { <div class="alert alert-info">No hay firmas locales que coincidan.</div> }
  @else if (rows.length) {
    <div class="waiter-result-heading"><div><span class="page-kicker">RESULTADOS</span><h2>{{ rows.length }} firmas encontradas</h2></div><small>Toque “Comparar” para fijar hasta dos firmas</small></div>
    <div class="row g-4">
      @for (r of rows; track r.id) {
        <div class="col-12 col-lg-6"><article class="card waiter-signature-card h-100"><div class="card-body">
          <div class="d-flex justify-content-between"><h5>{{ r.signerName }}</h5><span class="badge text-bg-secondary">{{ role(r.signerRole) }}</span></div>
          <div class="small text-muted mb-2">Reserva {{ r.confirmationNumber }} · Habitación {{ r.roomNumber || '—' }}</div>
          @if (images[r.id]) { <div class="waiter-signature-image"><img [src]="images[r.id]" [alt]="'Firma de ' + r.signerName"></div> }
          <dl class="row small mt-3 mb-0">
            <dt class="col-4">Firmada</dt><dd class="col-8">{{ dt(r.signedAtUtc) }}</dd>
            <dt class="col-4">Identidad</dt><dd class="col-8 text-break">{{ r.signerKey }}</dd>
            <dt class="col-4">Documento</dt><dd class="col-8">{{ r.documentVersion == null ? 'Pendiente' : 'Registration Card v' + r.documentVersion }}</dd>
            <dt class="col-4">Attachment</dt><dd class="col-8">{{ r.attachmentId || 'Pendiente de carga' }}</dd>
            <dt class="col-4">Hash</dt><dd class="col-8 text-break font-monospace">{{ r.signatureHash }}</dd>
          </dl>
          <button class="btn btn-outline-primary waiter-compare-button mt-3" (click)="toggleCompare(r)">{{ isCompared(r) ? '✓ Firma seleccionada' : 'Comparar firma' }}</button>
        </div></article></div>
      }
    </div>
  }`
})
export class FirmasComponent {
  private api = inject(ApiService);
  term = ''; loading = false; searched = false; error = '';
  rows: StoredSignatureInfo[] = [];
  images: Record<string, string> = {};
  compare: StoredSignatureInfo[] = [];

  search() {
    if (this.term.trim().length < 2) { this.error = 'Capture al menos dos caracteres.'; return; }
    this.loading = true; this.searched = true; this.error = ''; this.images = {};
    this.api.searchStoredSignatures(this.term.trim()).then(async rows => {
      this.rows = rows;
      for (const r of rows) {
        try {
          const b = await this.api.getSignatureImage(r.id);
          const buf = await b.arrayBuffer();
          let bin = ''; const bytes = new Uint8Array(buf);
          for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
          this.images[r.id] = 'data:image/png;base64,' + btoa(bin);
        } catch { /* sin imagen */ }
      }
      this.loading = false;
    }, e => { this.error = e?.error?.message || e.message; this.loading = false; });
  }
  isCompared(r: StoredSignatureInfo) { return this.compare.some(x => x.id === r.id); }
  toggleCompare(r: StoredSignatureInfo) {
    if (this.isCompared(r)) this.compare = this.compare.filter(x => x.id !== r.id);
    else { if (this.compare.length === 2) this.compare.shift(); this.compare.push(r); }
  }
  role(r: string) { return r === 'PrimaryGuest' ? 'Huésped principal' : 'Acompañante'; }
  dt(v: string) { return new Date(v).toLocaleString('es-MX', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }); }
}
