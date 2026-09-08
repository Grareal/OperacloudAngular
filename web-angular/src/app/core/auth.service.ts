import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable, of, switchMap, tap } from 'rxjs';
import { AuthSession } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private initialized = false;

  username = '';
  displayName = '';
  role = '';
  permissions: string[] = [];

  get isAuthenticated() { return !!this.username; }
  hasPermission(permission: string) { return this.permissions.includes(permission); }

  ensureSession(): Observable<boolean> {
    if (this.initialized) return of(this.isAuthenticated);
    return this.http.get<AuthSession>('/api/auth/session').pipe(
      tap(session => this.setSession(session)),
      map(() => true),
      catchError(() => {
        this.clear();
        return of(false);
      })
    );
  }

  login(username: string, password: string) {
    return this.refreshCsrf().pipe(
      switchMap(() => this.http.post<AuthSession>('/api/auth/login', { username, password })),
      tap(session => this.setSession(session))
    );
  }

  logout() {
    return this.refreshCsrf().pipe(
      switchMap(() => this.http.post<void>('/api/auth/logout', {})),
      catchError(() => of(undefined)),
      tap(() => this.clear())
    );
  }

  clear() {
    this.initialized = true;
    this.username = '';
    this.displayName = '';
    this.role = '';
    this.permissions = [];
  }

  private refreshCsrf() {
    return this.http.get<{ headerName: string }>('/api/auth/csrf');
  }

  private setSession(session: AuthSession) {
    this.initialized = true;
    this.username = session.username || '';
    this.displayName = session.displayName || '';
    this.role = session.role || '';
    this.permissions = session.permissions || [];
  }
}
