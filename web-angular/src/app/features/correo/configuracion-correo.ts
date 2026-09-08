import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { GuestEmailSettingInfo } from '../../core/models';

@Component({
  selector: 'app-config-correo',
  imports: [FormsModule],
  template: `
  <header class="mb-4"><div class="page-kicker">Administración local</div><h1 class="page-heading">Configuración de correo SMTP</h1><p class="page-subtitle">El servidor SMTP entrega la tarjeta y documentos al huésped.</p></header>
  @if (!isAdmin) { <div class="alert alert-warning">Esta sección requiere una cuenta de administrador.</div> }
  @else {
  <div class="alert" [class.alert-success]="model.smtpConfigured" [class.alert-warning]="!model.smtpConfigured"><strong>SMTP:</strong> {{ model.smtpConfigured ? 'configuración lista para usarse.' : 'complete servidor, puerto y remitente antes de activar los envíos.' }}</div>
  <section class="panel"><div class="row g-3">
    <div class="col-md-2"><label class="form-label">Hotel</label><input class="form-control" [(ngModel)]="model.hotelId"></div>
    <div class="col-md-3"><div class="form-check mt-4"><input id="enabled" class="form-check-input" type="checkbox" [(ngModel)]="model.enabled"><label for="enabled" class="form-check-label">Activar envíos automáticos</label></div></div>
    <div class="col-md-4"><label class="form-label">Servidor SMTP</label><input class="form-control" [(ngModel)]="model.host" placeholder="smtp.office365.com"></div>
    <div class="col-md-2"><label class="form-label">Puerto</label><input type="number" class="form-control" [(ngModel)]="model.port"></div>
    <div class="col-md-1"><div class="form-check mt-4"><input id="ssl" class="form-check-input" type="checkbox" [(ngModel)]="model.enableSsl"><label for="ssl" class="form-check-label">TLS</label></div></div>
    <div class="col-md-6"><label class="form-label">Usuario SMTP</label><input class="form-control" [(ngModel)]="model.username" autocomplete="username"></div>
    <div class="col-md-6"><label class="form-label">Contraseña SMTP</label><input type="password" class="form-control" [(ngModel)]="model.password" autocomplete="new-password" [placeholder]="model.passwordConfigured ? 'Deje vacío para conservarla' : 'Contraseña o app password'"></div>
    <div class="col-md-8"><label class="form-label">Buzón remitente</label><input type="email" class="form-control" [(ngModel)]="model.fromAddress" placeholder="correo@vidanta.com"></div>
    <div class="col-md-2"><label class="form-label">Nombre remitente</label><input class="form-control" [(ngModel)]="model.fromName"></div>
    <div class="col-md-2"><label class="form-label">Intentos</label><input type="number" class="form-control" [(ngModel)]="model.maxAttempts"></div>
    <div class="col-12"><label class="form-label">Asunto</label><input class="form-control" [(ngModel)]="model.subject"><div class="form-text">Variables: {{ '{confirmation}' }}</div></div>
    <div class="col-12"><label class="form-label">Cuerpo HTML</label><textarea class="form-control font-monospace" rows="6" [(ngModel)]="model.bodyHtml"></textarea><div class="form-text">Variables: {{ '{guest}' }} y {{ '{confirmation}' }}</div></div>
    <div class="col-md-4"><button class="btn btn-primary w-100" [disabled]="busy" (click)="save()">Guardar configuración</button></div>
    <div class="col-md-5"><input type="email" class="form-control" [(ngModel)]="testRecipient" placeholder="Destinatario de prueba"></div>
    <div class="col-md-3"><button class="btn btn-outline-primary w-100" [disabled]="busy || !model.smtpConfigured || !testRecipient.trim()" (click)="test()">Enviar prueba</button></div>
  </div></section>
  <section class="panel mt-4"><div class="page-kicker">Prueba sin envío</div><h2 class="section-title mt-2">Vista previa por reserva</h2><p class="text-muted">Muestra el correo y sus archivos sin conectarse al servidor SMTP.</p><div class="input-group"><input class="form-control" [(ngModel)]="previewConfirmation" placeholder="Número de reserva"><button class="btn btn-outline-primary" [disabled]="!previewConfirmation.trim()" (click)="preview()">Visualizar correo</button></div></section>
  @if (message) { <div class="alert mt-3" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  }`
})
export class ConfiguracionCorreoComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  auth = inject(AuthService);
  model: GuestEmailSettingInfo = { hotelId: 'VINV', enabled: false, smtpConfigured: false, host: '', port: 587, enableSsl: true, username: '', password: '', passwordConfigured: false, fromAddress: '', fromName: 'Vidanta', subject: '', bodyHtml: '', maxAttempts: 5 };
  testRecipient = ''; previewConfirmation = ''; message = '';
  busy = false; isError = false;
  get isAdmin() { return this.auth.role === 'Admin'; }

  ngOnInit() {
    if (!this.isAdmin) return;
    this.api.getEmailSetting('VINV').then(m => { if (m) this.model = m; }).catch(() => undefined);
  }
  save() {
    this.busy = true; this.isError = false;
    this.api.saveEmailSetting(this.model.hotelId, this.model).then(
      () => { this.model.password = ''; this.model.passwordConfigured = true; this.message = 'Configuración SMTP guardada.'; this.busy = false; },
      e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
    );
  }
  test() {
    this.busy = true; this.isError = false;
    this.api.saveEmailSetting(this.model.hotelId, this.model).then(() => {
      this.model.password = '';
      this.api.testEmailSetting(this.model.hotelId, this.testRecipient).then(
        r => { this.message = r.message; this.busy = false; },
        e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; }
      );
    }, e => { this.isError = true; this.message = e?.error?.message || e.message; this.busy = false; });
  }
  preview() { this.router.navigate(['/documentos-reserva'], { queryParams: { reserva: this.previewConfirmation.trim() } }); }
}
