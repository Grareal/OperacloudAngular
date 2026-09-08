import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { Reservation } from '../../core/models';

@Component({
  selector: 'app-huesped',
  imports: [FormsModule, RouterLink],
  template: `
  <div class="guest-welcome-shell">
    <section class="guest-welcome-visual">
      <div class="guest-glow"></div><div class="guest-monogram">V</div>
      <div class="guest-visual-copy"><span>VIDANTA</span><h1>Donde comienzan<br>historias extraordinarias.</h1><p>Su estancia, sus acompañantes y su registro en una experiencia simple y segura.</p></div>
    </section>
    <section class="guest-welcome-content">
      @if (!confirmation) {
        <div class="guest-entry">
          <div class="guest-eyebrow">REGISTRO DIGITAL</div>
          <h2>Encuentre su estancia</h2>
          <p class="guest-lead">Ingrese el número de confirmación que aparece en su correo o documento de reservación.</p>
          <form class="guest-entry-form" (ngSubmit)="find()">
            <label for="guest-confirmation">Número de reservación</label>
            <div class="guest-entry-control"><span>#</span><input id="guest-confirmation" inputmode="numeric" autocomplete="off" autofocus placeholder="Ej. 589474005" [(ngModel)]="entry" name="entry"></div>
            @if (entryError) { <div class="guest-entry-error" role="alert">{{ entryError }}</div> }
            <button type="submit" class="btn guest-primary-action" [disabled]="!entry.trim()">Continuar <span>→</span></button>
          </form>
          <div class="guest-entry-help"><span>i</span><p>Su número de confirmación contiene únicamente dígitos. Si no lo encuentra, solicite apoyo a recepción.</p></div>
        </div>
      } @else if (loading) { <div class="guest-loading"><div class="spinner-border"></div><span>Preparando su bienvenida…</span></div> }
      @else if (!reservation) {
        <div class="guest-not-found"><div class="guest-not-found-icon">!</div><h2>No encontramos la estancia</h2><p>{{ error || 'Verifique el número de reservación e intente nuevamente.' }}</p>
        <div class="d-flex flex-wrap justify-content-center gap-2"><a class="btn btn-outline-primary" routerLink="/huesped">Ingresar otro número</a></div></div>
      } @else {
        <div class="guest-eyebrow">BIENVENIDO A VIDANTA</div><h2>Hola, {{ firstName }}</h2>
        <p class="guest-lead">Antes de disfrutar su estancia, necesitamos confirmar algunos datos y registrar las firmas autorizadas.</p>
        <div class="guest-stay-card">
          <div><span>Confirmación</span><strong>{{ reservation.confirmationNumber }}</strong></div>
          <div><span>Habitación</span><strong>{{ v(reservation.roomStay.roomId) }}</strong></div>
          <div><span>Estancia</span><strong>{{ v(reservation.roomStay.arrivalDate) }} — {{ v(reservation.roomStay.departureDate) }}</strong></div>
          <div><span>Huéspedes</span><strong>{{ reservation.roomStay.adultCount }} adultos · {{ reservation.roomStay.childCount }} menores</strong></div>
        </div>
        <div class="guest-steps"><span><b>1</b> Confirmar datos</span><span><b>2</b> Registrar ocupantes</span><span><b>3</b> Firmar y revisar</span></div>
        <button type="button" class="btn guest-primary-action" (click)="begin()">Comenzar registro <span>→</span></button>
        <p class="guest-privacy">Sus datos y firmas se procesan de forma segura y se asocian únicamente con esta estancia.</p>
      }
    </section>
  </div>`
})
export class HuespedComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  confirmation = ''; entry = ''; entryError = '';
  reservation: Reservation | null = null;
  loading = false; error = '';

  ngOnInit() {
    this.route.params.subscribe(p => {
      this.confirmation = p['confirmation'] || '';
      if (this.confirmation) this.load();
    });
  }
  get firstName() { return this.reservation?.guest.givenName?.trim() ? this.reservation.guest.givenName : 'Huésped'; }
  load() {
    this.loading = true; this.reservation = null; this.error = '';
    this.api.getReservation(this.confirmation).then(
      l => { this.reservation = l[0] || null; this.loading = false; },
      e => { this.error = e?.error?.message || e.message; this.loading = false; }
    );
  }
  find() {
    const v = this.entry.trim();
    if (v.length < 5 || !/^\d+$/.test(v)) { this.entryError = 'Ingrese un número de reservación válido.'; return; }
    this.router.navigate(['/huesped', v]);
  }
  begin() { this.router.navigate(['/tarjeta', this.confirmation]); }
  v(x?: string) { return x?.trim() ? x : '—'; }
}
