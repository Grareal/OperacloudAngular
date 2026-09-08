import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { AccompanyingGuestChangePreview } from '../../core/models';

@Component({
  selector: 'app-acompanantes',
  imports: [FormsModule],
  template: `
  @if (!isAdmin) { <div class="alert alert-warning">Esta sección requiere una cuenta de administrador.</div> }
  @else {
  <header class="mb-4">
    <div class="page-kicker">OPERA Cloud UAT · Escritura controlada</div>
    <h1 class="page-heading">Agregar acompañante adulto</h1>
    <p class="page-subtitle">Primero se genera una comparación. Ningún dato cambia hasta confirmar expresamente el PUT.</p>
  </header>
  <div class="alert alert-warning">La primera versión solo admite perfiles Guest existentes, reservas <strong>Reserved</strong>, hotel <strong>VINV</strong> y máximo ocho adultos. No elimina acompañantes ni agrega menores.</div>
  <section class="panel mb-4">
    <div class="row g-3 align-items-end">
      <div class="col-md-5"><label class="form-label">Número de confirmación</label><input class="form-control" inputmode="numeric" [(ngModel)]="confirmation"></div>
      <div class="col-md-5"><label class="form-label">Profile ID adulto existente</label><input class="form-control" inputmode="numeric" [(ngModel)]="profileId"></div>
      <div class="col-md-2"><button class="btn btn-primary w-100" [disabled]="busy" (click)="preview()">{{ busy ? 'Consultando…' : 'Vista previa' }}</button></div>
    </div>
  </section>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  @if (pv) {
    <section class="panel mb-4">
      <div class="d-flex justify-content-between align-items-start mb-3">
        <div><div class="page-kicker">Comparación antes / después</div><h2 class="section-title mt-1">Reserva {{ pv.confirmationNumber }}</h2></div>
        <span class="badge" [class.text-bg-success]="pv.canApply" [class.text-bg-secondary]="!pv.canApply">{{ pv.reservationStatus }}</span>
      </div>
      <div class="row g-3 mb-4">
        <div class="col-md-4"><div class="metric-card"><div class="metric-label">Adultos actuales</div><div class="metric-value">{{ pv.currentAdults }}</div></div></div>
        <div class="col-md-4"><div class="metric-card"><div class="metric-label">Adultos propuestos</div><div class="metric-value">{{ pv.proposedAdults }}</div></div></div>
        <div class="col-md-4"><div class="metric-card"><div class="metric-label">Menores</div><div class="metric-value">{{ pv.children }}</div></div></div>
      </div>
      <div class="row g-4">
        <div class="col-md-6"><h3 class="h6">Perfiles actuales</h3><ul class="list-group">
          @for (g of pv.currentGuests; track g.profileId) { <li class="list-group-item d-flex justify-content-between"><span>{{ g.fullName }}<br><small class="text-muted">Profile {{ g.profileId }}</small></span><span>{{ g.primary ? 'Titular' : 'Acompañante' }}</span></li> }
        </ul></div>
        <div class="col-md-6"><h3 class="h6">Resultado propuesto</h3><ul class="list-group">
          @for (g of pv.proposedGuests; track g.profileId) { <li class="list-group-item d-flex justify-content-between" [class.list-group-item-success]="g.profileId === pv.requestedProfile.profileId && !pv.alreadyLinked"><span>{{ g.fullName }}<br><small class="text-muted">Profile {{ g.profileId }}</small></span><span>{{ g.primary ? 'Titular' : 'Acompañante' }}</span></li> }
        </ul></div>
      </div>
      <div class="alert mt-4" [class.alert-info]="pv.canApply" [class.alert-secondary]="!pv.canApply">{{ pv.validationMessage }}</div>
      @if (pv.canApply) {
        <div class="border rounded p-3 mt-3">
          <label class="form-label">Escriba <code>AGREGAR ACOMPAÑANTE</code> para habilitar la escritura</label>
          <div class="input-group"><input class="form-control" [(ngModel)]="confirmText"><button class="btn btn-danger" [disabled]="busy || confirmText !== 'AGREGAR ACOMPAÑANTE'" (click)="apply()">Confirmar PUT en UAT</button></div>
        </div>
      }
    </section>
  }
  }`
})
export class AcompanantesComponent {
  private api = inject(ApiService);
  auth = inject(AuthService);
  confirmation = ''; profileId = ''; confirmText = ''; message = '';
  busy = false; isError = false;
  pv: AccompanyingGuestChangePreview | null = null;
  get isAdmin() { return this.auth.role === 'Admin'; }

  preview() {
    this.message = ''; this.isError = false; this.pv = null;
    if (!/^\d+$/.test(this.confirmation) || !/^\d+$/.test(this.profileId)) { this.isError = true; this.message = 'Confirmación y Profile ID deben contener únicamente números.'; return; }
    this.busy = true;
    this.api.previewAddAccompanyingAdult(this.confirmation, this.profileId).then(
      p => { this.pv = p; this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  apply() {
    if (!this.pv?.canApply) return;
    this.busy = true; this.isError = false;
    this.api.addAccompanyingAdult(this.confirmation, this.profileId, this.pv.lastModifyDateTime, this.confirmText).then(
      r => { this.pv = r.after; this.confirmText = ''; this.message = `Acompañante agregado y verificado. Auditoría ${r.auditId}.`; this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
}
