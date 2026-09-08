import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { PromotionCatalogInfo } from '../../core/models';

@Component({
  selector: 'app-promociones',
  imports: [FormsModule],
  template: `
  <header class="mb-4"><div class="page-kicker">Administración local</div><h1 class="page-heading">Mantenimiento de promociones</h1>
  <p class="page-subtitle">Convierta los códigos de OPERA en información clara y aprobada para el huésped.</p></header>
  <div class="alert alert-info"><strong>OPERA es solo la fuente.</strong> Este catálogo se almacena localmente y no modifica reservas ni LOV en OPERA. Solo las entradas activas y aprobadas podrán aparecer en documentos.</div>
  <section class="panel mb-4">
    <div class="row g-3 align-items-end">
      <div class="col-md-2"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="hotel"></div>
      <div class="col-md-2"><label class="form-label">Idioma</label><select class="form-select" [(ngModel)]="language"><option>ES</option><option>EN</option></select></div>
      <div class="col-md-3"><label class="form-label">Estado</label><select class="form-select" [(ngModel)]="status"><option value="All">Todos</option><option value="Pending">Pendientes</option><option value="Approved">Aprobados</option><option value="Inactive">Inactivos</option></select></div>
      <div class="col-md-4"><label class="form-label">Buscar</label><input class="form-control" [(ngModel)]="search" placeholder="Código o descripción"></div>
      <div class="col-md-1"><button class="btn btn-primary w-100" (click)="load()">Buscar</button></div>
    </div>
    <hr>
    <div class="row g-3 align-items-end">
      <div class="col-md-8"><label class="form-label">Importar exportación CSV de OPERA</label><input type="file" class="form-control" accept=".csv,.txt" (change)="onImport($event)"><div class="form-text">Se importará solamente la entidad VIDA_PROMOTIONSTSW. No reemplaza textos para huésped ya capturados.</div></div>
      <div class="col-md-4"><button class="btn btn-outline-primary w-100" [disabled]="!importFile || busy" (click)="doImport()">Importar y detectar códigos</button></div>
    </div>
  </section>
  <section class="panel mb-4">
    <div class="d-flex justify-content-between align-items-center mb-3"><div><div class="page-kicker">Edición</div><h2 class="section-title mb-0">{{ editingId ? 'Editar promoción' : 'Nueva promoción' }}</h2></div><button class="btn btn-sm btn-outline-secondary" (click)="fresh()">Limpiar</button></div>
    <div class="row g-3">
      <div class="col-md-3"><label class="form-label">Código OPERA</label><input class="form-control" [(ngModel)]="edit.operaCode"></div>
      <div class="col-md-9"><label class="form-label">Descripción original de OPERA</label><input class="form-control" [(ngModel)]="edit.operaDescription"></div>
      <div class="col-md-4"><label class="form-label">Título para el huésped</label><input class="form-control" [(ngModel)]="edit.guestTitle" placeholder="Ej. Desayuno incluido"></div>
      <div class="col-md-8"><label class="form-label">Descripción para el huésped</label><textarea class="form-control" rows="3" [(ngModel)]="edit.guestDescription" placeholder="Texto claro, comercial y aprobado"></textarea></div>
      <div class="col-md-2"><label class="form-label">Orden</label><input type="number" class="form-control" [(ngModel)]="edit.sortOrder"></div>
      <div class="col-md-3"><div class="form-check mt-4"><input class="form-check-input" type="checkbox" [(ngModel)]="edit.isActive" id="pa"><label class="form-check-label" for="pa">Activa</label></div></div>
      <div class="col-md-4"><div class="form-check mt-4"><input class="form-check-input" type="checkbox" [(ngModel)]="edit.isApprovedForGuest" id="pap"><label class="form-check-label" for="pap">Aprobada para mostrar al huésped</label></div></div>
      <div class="col-md-3"><button class="btn btn-primary w-100 mt-4" [disabled]="busy" (click)="save()">Guardar promoción</button></div>
    </div>
  </section>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  <section class="panel">
    <div class="d-flex justify-content-between mb-3"><h2 class="section-title mb-0">Catálogo</h2><span class="badge text-bg-secondary">{{ items.length }} registros</span></div>
    <div class="table-responsive"><table class="table align-middle"><thead><tr><th>Código</th><th>Referencia OPERA</th><th>Información para huésped</th><th>Estado</th><th></th></tr></thead><tbody>
      @for (it of items; track it.id) {
        <tr><td><strong>{{ it.operaCode }}</strong><br><small>{{ it.language }} · {{ it.source }}</small></td><td class="small">{{ it.operaDescription }}</td><td><strong>{{ it.guestTitle }}</strong><div class="small">{{ it.guestDescription }}</div></td>
        <td>@if (it.isApprovedForGuest && it.isActive) { <span class="badge text-bg-success">Aprobada</span> } @else if (!it.isActive) { <span class="badge text-bg-secondary">Inactiva</span> } @else { <span class="badge text-bg-warning">Pendiente</span> }</td>
        <td><button class="btn btn-sm btn-outline-primary" (click)="editItem(it)">Editar</button></td></tr>
      }
    </tbody></table></div>
  </section>`
})
export class PromocionesComponent implements OnInit {
  private api = inject(ApiService);
  hotel = 'VINV'; language = 'ES'; status = 'All'; search = ''; message = '';
  busy = false; isError = false;
  items: PromotionCatalogInfo[] = [];
  editingId: string | null = null;
  importFile: File | null = null;
  edit: any = {};

  ngOnInit() { this.fresh(); this.load(); }
  fresh() { this.editingId = null; this.edit = { hotelId: this.hotel, language: this.language, operaCode: '', operaDescription: '', guestTitle: '', guestDescription: '', sortOrder: 0, isActive: true, isApprovedForGuest: false }; }
  load() { this.api.getPromotionCatalog(this.hotel, this.language, this.search, this.status).then(r => this.items = r); }
  editItem(x: PromotionCatalogInfo) { this.editingId = x.id; this.edit = { ...x }; }
  onImport(e: Event) { this.importFile = (e.target as HTMLInputElement).files?.[0] || null; }
  save() {
    this.busy = true; this.isError = false;
    this.edit.hotelId = this.hotel; this.edit.language = this.language;
    const op = this.editingId ? this.api.updatePromotionCatalogEntry(this.editingId, this.edit) : this.api.savePromotionCatalogEntry(this.edit);
    op.then(
      () => { this.message = 'Promoción guardada.'; this.fresh(); this.load(); this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  doImport() {
    if (!this.importFile) return;
    this.busy = true; this.isError = false;
    this.api.importPromotionCatalog(this.hotel, this.language, this.importFile).then(
      r => { this.message = r.message || 'Importación terminada.'; this.load(); this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
}
