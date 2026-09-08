import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-historial',
  imports: [],
  template: `
  <h3>Historial de documentos</h3>
  <p class="text-muted">Documentos definitivos generados y enviados a OPERA (BD local).</p>
  @if (loading) { <div class="spinner-border text-primary" role="status"></div> <span>Cargando…</span> }
  @else if (error) { <div class="alert alert-danger">{{ error }}</div> }
  @else {
    <div class="table-responsive">
      <table class="table table-sm table-hover align-middle">
        <thead><tr><th>Conf.</th><th>Habitación</th><th>Versión</th><th>Estado</th><th>Generada</th><th>OPERA</th><th></th></tr></thead>
        <tbody>
          @for (d of docs; track d.id) {
            <tr>
              <td>{{ d.confirmationNumber }}</td>
              <td>{{ d.roomNumber || '—' }}</td>
              <td>{{ d.version }}</td>
              <td><span class="badge" [class.text-bg-success]="d.status === 'Uploaded'" [class.text-bg-info]="d.status !== 'Uploaded'">{{ d.status }}</span></td>
              <td>{{ dt(d.createdAtUtc) }}</td>
              <td>{{ d.uploadedAtUtc ? 'Adjunto ' + d.attachmentId : 'Pendiente' }}</td>
              <td><button class="btn btn-sm btn-outline-primary" (click)="open(d.confirmationNumber)">Ver</button></td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (!docs.length) { <p class="text-muted">Aún no hay documentos definitivos almacenados.</p> }
  }`
})
export class HistorialComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  loading = true; error = ''; docs: any[] = [];
  ngOnInit() {
    this.api.getRecentLocalDocuments(200).then(
      d => { this.docs = d; this.loading = false; },
      e => { this.error = e?.error?.message || e.message; this.loading = false; }
    );
  }
  open(c: string) { this.router.navigate(['/documentos-reserva'], { queryParams: { reserva: c } }); }
  dt(v: string) { return new Date(v).toLocaleString('es-MX', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }); }
}
