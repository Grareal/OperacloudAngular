import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { PdfTemplateSummary } from '../../core/models';

@Component({
  selector: 'app-admin',
  imports: [FormsModule, RouterLink],
  template: `
  <header class="mb-4"><div class="page-kicker">Administración local</div><h1 class="page-heading">Configuración y control documental</h1><p class="page-subtitle">Gestione plantillas, versiones, documentos y auditoría del hotel.</p></header>
  @if (!isAdmin) { <div class="alert alert-warning">Esta sección requiere una cuenta de administrador.</div> }
  @else {
  <div class="admin-module-grid">
    <a class="admin-module" routerLink="/usuarios"><span class="admin-module-icon">👥</span><strong>Usuarios y grupos</strong><small>Credenciales, grupos y acceso por vista.</small></a>
    <a class="admin-module" routerLink="/plantillas-pdf"><span class="admin-module-icon">▤</span><strong>Plantillas PDF</strong><small>Subir, mapear, probar y publicar tarjetas por hotel.</small></a>
    <a class="admin-module" routerLink="/documentos-correo"><span class="admin-module-icon">✉</span><strong>Documentos por correo</strong><small>Avisos, reglamentos, promociones, vigencias e idiomas.</small></a>
    <a class="admin-module" routerLink="/documentos-reserva"><span class="admin-module-icon">▤</span><strong>Expediente documental</strong><small>Consultar todos los archivos, firmas y correos de una reserva.</small></a>
    <a class="admin-module" routerLink="/configuracion-correo"><span class="admin-module-icon">@@</span><strong>Correo SMTP</strong><small>Servidor, remitente, contenido y pruebas de envío.</small></a>
    <a class="admin-module" routerLink="/promociones"><span class="admin-module-icon">★</span><strong>Catálogo de promociones</strong><small>Códigos OPERA y textos aprobados para el huésped.</small></a>
    <a class="admin-module" routerLink="/codigos-promocion"><span class="admin-module-icon admin-module-icon-text">PRO</span><strong>Códigos de promoción</strong><small>Consultar catálogos OPERA, códigos UDFC02 y su uso en reservas.</small></a>
    <a class="admin-module" routerLink="/acompanantes-opera"><span class="admin-module-icon">+</span><strong>Acompañantes OPERA</strong><small>Previsualizar y agregar perfiles adultos con verificación y auditoría UAT.</small></a>
    <a class="admin-module" routerLink="/historial"><span class="admin-module-icon">≡</span><strong>Historial</strong><small>Consultar las tarjetas almacenadas y sus estados.</small></a>
    <a class="admin-module" routerLink="/consulta-firmas"><span class="admin-module-icon">✍</span><strong>Auditoría de firmas</strong><small>Buscar firmantes y comparar evidencias.</small></a>
    <a class="admin-module" routerLink="/operacion"><span class="admin-module-icon">⌕</span><strong>Probar operación</strong><small>Abrir el flujo móvil de búsqueda y firma.</small></a>
  </div>
  <section class="panel mt-4"><div class="page-kicker">Generación documental</div><h2 class="section-title mt-2 mb-3">Origen de la Registration Card</h2>
    <div class="row g-3 align-items-end">
      <div class="col-md-3"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="hotelId"></div>
      <div class="col-md-4"><label class="form-label">Modo</label><select class="form-select" [(ngModel)]="sourceMode"><option value="Auto">Automático</option><option value="Local">Usar plantilla local</option><option value="Opera">Usar plantilla de OPERA</option></select></div>
      <div class="col-md-4"><label class="form-label">Plantilla local preferida</label><select class="form-select" [(ngModel)]="preferred"><option value="">Selección por reglas</option>@for (t of templates; track t.id) { @if (t.isPublished && t.templateType === 'RegistrationCard') { <option [value]="t.id">{{ t.name }} v{{ t.version }}</option> } }</select></div>
      <div class="col-md-1"><button type="button" class="btn btn-primary" (click)="save()">Guardar</button></div>
    </div>
    <div class="form-text mt-3"><strong>Automático:</strong> usa una local compatible y OPERA como respaldo. <strong>Local:</strong> exige una publicada. <strong>OPERA:</strong> ignora PDFs locales.</div>
    @if (message) { <div class="alert alert-info mt-3 mb-0">{{ message }}</div> }
  </section>
  }`
})
export class AdminComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  hotelId = 'VINV'; sourceMode = 'Auto'; preferred = ''; message = '';
  templates: PdfTemplateSummary[] = [];
  get isAdmin() { return this.auth.role === 'Admin'; }
  ngOnInit() {
    if (!this.isAdmin) return;
    this.api.getPdfTemplates().then(t => {
      this.templates = t;
      this.api.getHotelDocumentSetting(this.hotelId).then(s => {
        if (s) { this.sourceMode = s.sourceMode; this.preferred = s.preferredPdfTemplateId || ''; }
      }).catch(() => undefined);
    }).catch(() => undefined);
  }
  save() {
    this.api.saveHotelDocumentSetting(this.hotelId, this.sourceMode, this.preferred || undefined).then(
      () => this.message = 'Configuración guardada. Se aplicará en la siguiente vista previa y documento.',
      e => this.message = e?.error?.message || e.message
    );
  }
}
