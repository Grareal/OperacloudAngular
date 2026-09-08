import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { OperaPromotionCodeInfo, PromotionCatalogInfo, Reservation } from '../../core/models';

interface Row {
  code: string; name: string; description: string; groupCode: string; groupName: string;
  bookingDates: string; stayDates: string; official: boolean; commercial: boolean; assigned: boolean;
  occurrences: number; examples: string[]; rates: { code: string; description: string }[];
}

@Component({
  selector: 'app-codigos',
  imports: [FormsModule, RouterLink],
  template: `
  @if (!isAdmin) { <div class="alert alert-warning">Esta sección requiere una cuenta de administrador.</div> }
  @else {
  <header class="udf-heading mb-4">
    <div><div class="page-kicker">OPERA Cloud · Reservaciones</div><h1 class="page-heading">Explorador de códigos de promoción</h1>
    <p class="page-subtitle">Catálogo oficial de OPERA, códigos comerciales de VIDA y promociones observadas en reservas.</p></div>
    <span class="udf-readonly-badge"><b></b> OHIP solo lectura</span>
  </header>
  <div class="promotion-source-note mb-4">
    <div><strong>Son dos fuentes distintas.</strong><span><b>Promotion Codes de OPERA</b> son campañas tarifarias oficiales. Los valores como <code>INTERCONEXION</code> llegan desde el campo comercial <code>UDFC02</code> de la reserva y se traducen con el catálogo VIDA.</span></div>
    <a class="btn btn-sm btn-outline-primary" routerLink="/promociones">Importar / editar catálogo VIDA</a>
  </div>
  <section class="udf-search-panel mb-3">
    <div class="udf-search-copy"><span class="udf-search-mark">P</span><div><strong>Promociones asignadas a una reserva</strong><small>Consulta el valor real de UDFC02 y lo cruza con ambos catálogos.</small></div></div>
    <div class="udf-search-controls">
      <input class="form-control" [(ngModel)]="confirmation" placeholder="Número de confirmación">
      <button class="btn btn-primary" [disabled]="busy || !confirmation.trim()" (click)="loadReservation()">Consultar reserva</button>
    </div>
  </section>
  <section class="panel promotion-scan-panel mb-4">
    <div><div class="page-kicker">Descubrimiento</div><strong>Analizar códigos usados en llegadas</strong><small>Revisa hasta 500 reservas del rango mediante GET y cuenta en cuáles aparece cada código de UDFC02.</small></div>
    <label><span>Desde</span><input type="date" class="form-control" [(ngModel)]="from"></label>
    <label><span>Hasta</span><input type="date" class="form-control" [(ngModel)]="to"></label>
    <button class="btn btn-outline-primary" [disabled]="busy" (click)="scan()">Analizar rango</button>
  </section>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-info]="!isError">{{ message }}</div> }
  @if (reservation) {
    <section class="udf-reservation-strip mb-4">
      <div><span>Reserva</span><strong>{{ reservation.confirmationNumber }}</strong></div>
      <div><span>Huésped</span><strong>{{ reservation.guest.fullName || 'Sin nombre' }}</strong></div>
      <div><span>Habitación / tipo</span><strong>{{ room() }}</strong></div>
      <div><span>Códigos en UDFC02</span><strong>{{ resCodes.length ? resCodes.join(', ') : 'Sin promociones' }}</strong></div>
      <div><span>Fuente técnica</span><strong>userDefinedFields.characterUDFs · UDFC02</strong></div>
    </section>
  }
  <div class="row g-3 mb-4">
    <div class="col-6 col-lg-3"><div class="metric-card"><div class="metric-label">Códigos consolidados</div><div class="metric-value">{{ rows().length }}</div><div class="metric-note">Sin duplicar entre fuentes</div></div></div>
    <div class="col-6 col-lg-3"><div class="metric-card"><div class="metric-label">Oficiales OPERA</div><div class="metric-value">{{ opera.length }}</div><div class="metric-note">OHIP getPromotionCodes</div></div></div>
    <div class="col-6 col-lg-3"><div class="metric-card"><div class="metric-label">Catálogo comercial</div><div class="metric-value">{{ local.length }}</div><div class="metric-note">VIDA / UDFC02 importado localmente</div></div></div>
    <div class="col-6 col-lg-3"><div class="metric-card"><div class="metric-label">Asignados en consulta</div><div class="metric-value">{{ resCodes.length }}</div><div class="metric-note">Reserva seleccionada</div></div></div>
  </div>
  <section class="panel udf-catalog-panel">
    <div class="udf-toolbar">
      <div><div class="page-kicker">Catálogo consolidado</div><h2 class="section-title mt-1">Códigos disponibles y observados</h2></div>
      <div class="promotion-code-filters">
        <input class="form-control" [(ngModel)]="filter" placeholder="Buscar código, nombre, grupo o descripción">
        <select class="form-select" [(ngModel)]="sourceFilter"><option value="All">Todas las fuentes</option><option value="Assigned">Asignados a la reserva</option><option value="Official">Oficiales OPERA</option><option value="Commercial">Catálogo comercial</option><option value="Observed">Observados en rango</option></select>
      </div>
    </div>
    <div class="udf-legend"><span><i class="udf-dot confirmed"></i> OPERA oficial</span><span><i class="udf-dot inferred"></i> Catálogo comercial UDFC02</span><span><i class="udf-dot unknown"></i> Detectado en reservas</span></div>
    <div class="promotion-code-grid">
      @for (it of visible(); track it.code) {
        <article class="promotion-code-card" [class.assigned]="it.assigned">
          <div class="promotion-code-head">
            <div><code>{{ it.code }}</code>@if (it.assigned) { <span class="badge text-bg-success">En esta reserva</span> }</div>
            <div class="promotion-source-badges">@if (it.official) { <span class="official">OPERA</span> }@if (it.commercial) { <span class="commercial">UDFC02</span> }@if (it.occurrences) { <span class="observed">{{ it.occurrences }} usos</span> }</div>
          </div>
          <h3>{{ it.name || 'Sin descripción cargada' }}</h3>
          @if (it.description) { <p>{{ it.description }}</p> }
          @if (it.groupName) { <dl><dt>Grupo</dt><dd>{{ it.groupCode }} · {{ it.groupName }}</dd></dl> }
          @if (it.examples.length) { <footer><span>Reservas de ejemplo</span><strong>{{ it.examples.join(', ') }}</strong></footer> }
        </article>
      }
    </div>
    @if (!visible().length) { <div class="empty-state">No hay códigos que coincidan con el filtro.</div> }
  </section>
  <div class="udf-footnote mt-4"><strong>Cobertura del catálogo.</strong> La sección OPERA es completa para <code>getPromotionCodes</code> del hotel seleccionado. Para que la parte comercial UDFC02 sea completa se debe importar la exportación vigente de <code>VIDA_PROMOTIONSTSW</code>; el análisis por fechas descubre códigos realmente usados, pero no sustituye la fuente maestra.</div>
  }`
})
export class CodigosComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  confirmation = '589474005'; filter = ''; sourceFilter = 'All'; message = '';
  from = '2026-08-01'; to = '2026-10-31';
  busy = false; isError = false;
  reservation: Reservation | null = null;
  local: PromotionCatalogInfo[] = [];
  opera: OperaPromotionCodeInfo[] = [];
  resCodes: string[] = [];
  observed = new Map<string, Set<string>>();
  get isAdmin() { return this.auth.role === 'Admin'; }

  ngOnInit() { if (this.isAdmin) this.loadCatalogs(); }

  rows(): Row[] {
    const map = new Map<string, Row>();
    const row = (c: string): Row => {
      let r = map.get(c.toLowerCase());
      if (!r) { r = { code: c, name: '', description: '', groupCode: '', groupName: '', bookingDates: '', stayDates: '', official: false, commercial: false, assigned: false, occurrences: 0, examples: [], rates: [] }; map.set(c.toLowerCase(), r); }
      return r;
    };
    for (const it of this.local) { const r = row(it.operaCode); r.commercial = true; r.name = r.name || it.guestTitle || it.operaDescription; r.description = r.description || it.guestDescription || it.operaDescription; }
    for (const it of this.opera) { const r = row(it.code); r.official = true; r.name = it.name || r.name; r.description = it.description || r.description; r.groupCode = it.groupCode; r.groupName = it.groupName; r.rates = it.rates; }
    for (const [k, v] of this.observed) { const r = row(k); r.occurrences = v.size; r.examples = [...v].slice(0, 4); }
    for (const c of this.resCodes) row(c).assigned = true;
    return [...map.values()].sort((a, b) => Number(b.assigned) - Number(a.assigned) || a.code.localeCompare(b.code));
  }
  visible() {
    const f = this.filter.trim().toLowerCase();
    return this.rows().filter(x =>
      (this.sourceFilter === 'All' || this.sourceFilter === 'Assigned' && x.assigned || this.sourceFilter === 'Official' && x.official || this.sourceFilter === 'Commercial' && x.commercial || this.sourceFilter === 'Observed' && x.occurrences > 0) &&
      (!f || `${x.code} ${x.name} ${x.description} ${x.groupCode} ${x.groupName}`.toLowerCase().includes(f)));
  }
  room() { const v = [this.reservation?.roomStay.roomId, this.reservation?.roomStay.roomType].filter(x => x?.trim()).join(' / '); return v || '—'; }

  loadCatalogs() {
    this.busy = true;
    Promise.all([this.api.getPromotionCatalog('VINV', 'ES', '', 'All'), this.api.getOperaPromotionCodes('VINV')]).then(
      ([l, o]) => { this.local = l; this.opera = o; this.message = `Catálogos actualizados: ${o.length} códigos oficiales OPERA y ${l.length} comerciales.`; this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  loadReservation() {
    this.busy = true; this.isError = false;
    this.api.getReservation(this.confirmation.trim()).then(
      l => {
        this.reservation = l[0] || null;
        this.resCodes = this.reservation ? this.codes(this.reservation) : [];
        this.message = this.resCodes.length ? `Se encontraron ${this.resCodes.length} códigos asignados.` : 'La reserva no devolvió códigos en UDFC02.';
        this.busy = false;
      },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  scan() {
    if (this.to < this.from) { this.isError = true; this.message = 'La fecha final debe ser igual o posterior a la inicial.'; return; }
    this.busy = true; this.isError = false; this.observed.clear();
    this.api.getArrivals(this.from, this.to, 500).then(
      list => {
        for (const r of list) for (const c of this.codes(r)) {
          let s = this.observed.get(c.toLowerCase());
          if (!s) this.observed.set(c.toLowerCase(), s = new Set());
          if (r.confirmationNumber) s.add(r.confirmationNumber);
        }
        this.message = `Análisis GET completado: ${list.length} reservas revisadas y ${this.observed.size} códigos distintos encontrados.`;
        this.busy = false;
      },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  private codes(r: Reservation): string[] {
    const udf = r.userDefinedFields?.find(x => x.name?.toLowerCase() === 'udfc02');
    return udf?.value?.split(/[,;]/).map(x => x.trim()).filter(x => x) || [];
  }
}
