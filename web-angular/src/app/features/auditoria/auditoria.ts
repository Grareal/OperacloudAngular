import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-auditoria',
  imports: [FormsModule],
  template: `
    <header class="mb-4"><div class="page-kicker">Trazabilidad legal</div><h1 class="page-heading">Auditoría de accesos y acciones</h1><p class="page-subtitle">Los eventos son append-only y están enlazados por SHA-256.</p></header>
    <section class="panel mb-4">
      <div class="row g-3 align-items-end">
        <div class="col-md-4"><label class="form-label">Reserva</label><input class="form-control" [(ngModel)]="confirmation"></div>
        <div class="col-md-4"><label class="form-label">Usuario</label><input class="form-control" [(ngModel)]="actor"></div>
        <div class="col-md-4 d-flex gap-2"><button class="btn btn-primary" [disabled]="busy" (click)="search()">Consultar</button><button class="btn btn-outline-secondary" [disabled]="busy" (click)="integrity()">Verificar cadena</button></div>
      </div>
      @if (message) { <div class="alert mt-3 mb-0" [class.alert-success]="valid" [class.alert-danger]="!valid">{{ message }}</div> }
    </section>
    <section class="panel">
      <div class="table-responsive"><table class="table align-middle"><thead><tr><th>Fecha UTC</th><th>Usuario</th><th>Acción</th><th>Recurso</th><th>Reserva</th><th>Motivo</th><th>Correlación</th></tr></thead><tbody>
        @for (e of rows; track e.id) { <tr><td>{{ e.occurredAtUtc }}</td><td>{{ e.actorUsername }}</td><td>{{ e.action }}</td><td>{{ e.resourceType }}<br><small>{{ e.resourceId }}</small></td><td>{{ e.confirmationNumber || '—' }}</td><td>{{ e.accessReason || '—' }}</td><td><code>{{ e.correlationId }}</code></td></tr> }
      </tbody></table></div>
      @if (!rows.length && !busy) { <p class="text-muted mb-0">No hay eventos para los filtros indicados.</p> }
    </section>`
})
export class AuditoriaComponent {
  private readonly api = inject(ApiService);
  confirmation = ''; actor = ''; busy = false; rows: any[] = []; message = ''; valid = true;

  search() {
    this.busy = true; this.message = '';
    this.api.searchAudit(this.confirmation.trim(), this.actor.trim()).then(
      rows => { this.rows = rows; this.busy = false; },
      error => { this.message = error?.error?.message || error.message; this.valid = false; this.busy = false; }
    );
  }

  integrity() {
    this.busy = true;
    this.api.verifyAuditIntegrity().then(result => {
      this.valid = result.valid;
      this.message = result.valid ? `Cadena válida (${result.count} eventos).` : `Cadena alterada a partir de ${result.brokenAt}.`;
      this.busy = false;
    }, error => { this.valid = false; this.message = error?.error?.message || error.message; this.busy = false; });
  }
}
