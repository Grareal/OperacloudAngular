import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ViewPermissions } from './core/models';
import { ApiService } from './core/api.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  auth = inject(AuthService);
  private router = inject(Router);
  private api = inject(ApiService);
  env = '';

  constructor() {
    this.api.getEnvironment().then(info => this.env = info.isUat ? 'UAT' : info.environment);
  }

  has(p: string) { return this.auth.hasPermission(p) || this.auth.role === 'Admin'; }
  get initial() { return (this.auth.displayName || 'U').slice(0, 1).toUpperCase(); }
  logout() {
    this.auth.logout().subscribe(() => void this.router.navigate(['/login']));
  }
  get showNav() { return this.auth.isAuthenticated && !this.router.url.startsWith('/login'); }
  get isUat() { return this.env === 'UAT'; }
}
