import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { Reservation } from '../../core/models';

@Component({
  selector: 'app-search',
  imports: [FormsModule],
  template: `
  <div class="d-flex flex-wrap justify-content-between align-items-start gap-3 mb-4">
    <div>
      <div class="page-kicker">OPERA Cloud · Operación móvil</div>
      <h1 class="page-heading">Registro y firma del huésped</h1>
      <p class="page-subtitle">Localiza la estancia, revisa los datos y entrega este mismo dispositivo al huésped.</p>
    </div>
    <span class="opera-status"><span class="status-dot"></span>Conexión segura</span>
  </div>
  <section class="panel search-hero mb-4">
    <form (ngSubmit)="search()">
      <label for="reservation-search" class="form-label fw-semibold">Confirmación o apellido del huésped</label>
      <div class="input-group input-group-lg">
        <input id="reservation-search" class="form-control" autocomplete="off" placeholder="Ej. 595046605 o Salazar" [(ngModel)]="term" name="term">
        <button class="btn btn-primary" type="submit" [disabled]="searching || !term.trim()">{{ searching ? 'Consultando…' : 'Buscar en OPERA' }}</button>
      </div>
      <div class="form-text mt-2">Para obtener un resultado exacto, utiliza el número de confirmación.</div>
    </form>
  </section>
  @if (searching) {
    <div class="panel empty-state" role="status"><div class="spinner-border text-primary mb-3"></div><div>Consultando OPERA Cloud…</div></div>
  } @else if (error) {
    <div class="alert alert-danger" role="alert"><strong>No fue posible consultar la reserva.</strong><div>{{ error }}</div><button class="btn btn-outline-danger mt-3" (click)="search()">Reintentar</button></div>
  } @else if (searched) {
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h2 class="section-title">Reservas encontradas</h2><span class="badge text-bg-light">{{ reservations.length }} resultados</span>
    </div>
    @if (!reservations.length) {
      <div class="panel empty-state"><h2 class="section-title mb-2">Sin coincidencias</h2><p class="mb-0">Verifica la confirmación o intenta con el apellido registrado en OPERA.</p></div>
    } @else {
      <div class="reservation-results">
        @for (r of reservations; track r.confirmationNumber) {
          <article class="panel reservation-result">
            <div class="reservation-result-main">
              <div class="small text-secondary">{{ r.confirmationNumber }}</div>
              <h2 class="section-title mt-1">{{ r.guest.fullName }}</h2>
              <div class="reservation-result-meta">
                <span>Hab. {{ dash(r.roomStay.roomId) }}</span>
                <span>{{ dash(r.roomStay.roomType) }}</span>
                <span>{{ dash(r.roomStay.arrivalDate) }} → {{ dash(r.roomStay.departureDate) }}</span>
              </div>
            </div>
            <button class="btn btn-primary" (click)="open(r)">Iniciar llenado y firma</button>
          </article>
        }
      </div>
    }
  }`
})
export class SearchComponent {
  private api = inject(ApiService);
  private router = inject(Router);
  term = ''; searching = false; searched = false; error = '';
  reservations: Reservation[] = [];

  search() {
    if (!this.term.trim()) return;
    this.searching = true; this.searched = true; this.error = ''; this.reservations = [];
    this.api.searchReservations(this.term.trim()).then(
      r => { this.reservations = r; this.searching = false; },
      e => { this.error = e?.error?.message || e.message; this.searching = false; }
    );
  }
  open(r: Reservation) {
    if (r.confirmationNumber) this.router.navigate(['/huesped', r.confirmationNumber]);
  }
  dash(v?: string) { return v?.trim() ? v : '—'; }
}
