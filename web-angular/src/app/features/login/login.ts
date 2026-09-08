import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
  <div class="login-shell">
    <div class="login-brand">
      <div><span class="brand-mark">V</span></div>
      <h1>Firma Vidanta</h1>
      <p>Registro digital · OPERA Cloud</p>
    </div>
    <div class="login-form">
      <h2>Iniciar sesión</h2>
      @if (error) { <div class="alert alert-danger">{{ error }}</div> }
      <form (ngSubmit)="submit()">
        <div class="mb-3"><label class="form-label">Usuario</label><input class="form-control" [(ngModel)]="username" name="u" autocomplete="username" required></div>
        <div class="mb-3"><label class="form-label">Contraseña</label><input class="form-control" type="password" [(ngModel)]="password" name="p" autocomplete="current-password" required></div>
        <button class="btn btn-primary w-100" [disabled]="busy">{{ busy ? 'Verificando…' : 'Entrar' }}</button>
      </form>
    </div>
  </div>`,
  styles: [`.login-form { max-width: 420px; margin: auto; padding: 2rem; }`]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  username = ''; password = ''; busy = false; error = '';

  submit() {
    this.busy = true; this.error = '';
    this.auth.login(this.username.trim(), this.password).subscribe({
      next: () => {
        const ret = this.route.snapshot.queryParams['returnUrl'] || '/';
        this.router.navigateByUrl(ret);
      },
      error: () => { this.error = 'Usuario o contraseña inválidos.'; this.busy = false; }
    });
  }
}
