import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { ViewPermissions as P } from './core/models';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login').then(m => m.LoginComponent) },
  { path: '', loadComponent: () => import('./features/home/home').then(m => m.HomeComponent), canActivate: [authGuard] },
  { path: 'busqueda', loadComponent: () => import('./features/search/search').then(m => m.SearchComponent), canActivate: [authGuard], data: { permission: P.RegistrationCard } },
  { path: 'operacion', loadComponent: () => import('./features/search/search').then(m => m.SearchComponent), canActivate: [authGuard], data: { permission: P.Operation } },
  { path: 'tarjeta/:confirmation', loadComponent: () => import('./features/tarjeta/tarjeta').then(m => m.TarjetaComponent), canActivate: [authGuard], data: { permission: P.RegistrationCard } },
  { path: 'documentos-reserva', loadComponent: () => import('./features/expediente/expediente').then(m => m.ExpedienteComponent), canActivate: [authGuard], data: { permission: P.Documents } },
  { path: 'consulta-firmas', loadComponent: () => import('./features/firmas/firmas').then(m => m.FirmasComponent), canActivate: [authGuard], data: { permission: P.SignatureLookup } },
  { path: 'meseros', loadComponent: () => import('./features/firmas/firmas').then(m => m.FirmasComponent), canActivate: [authGuard], data: { permission: P.SignatureLookup } },
  { path: 'historial', loadComponent: () => import('./features/historial/historial').then(m => m.HistorialComponent), canActivate: [authGuard], data: { permission: P.History } },
  { path: 'auditoria', loadComponent: () => import('./features/auditoria/auditoria').then(m => m.AuditoriaComponent), canActivate: [authGuard], data: { permission: P.Audit } },
  { path: 'plantillas-pdf', loadComponent: () => import('./features/plantillas/plantillas').then(m => m.PlantillasComponent), canActivate: [authGuard], data: { permission: P.PdfTemplates } },
  { path: 'documentos-correo', loadComponent: () => import('./features/correo/documentos-correo').then(m => m.DocumentosCorreoComponent), canActivate: [authGuard], data: { permission: P.Communications } },
  { path: 'configuracion-correo', loadComponent: () => import('./features/correo/configuracion-correo').then(m => m.ConfiguracionCorreoComponent), canActivate: [authGuard], data: { permission: P.EmailSettings } },
  { path: 'promociones', loadComponent: () => import('./features/promociones/promociones').then(m => m.PromocionesComponent), canActivate: [authGuard], data: { permission: P.Promotions } },
  { path: 'codigos-promocion', loadComponent: () => import('./features/promociones/codigos').then(m => m.CodigosComponent), canActivate: [authGuard], data: { permission: P.Promotions } },
  { path: 'udf', redirectTo: 'codigos-promocion', pathMatch: 'full' },
  { path: 'acompanantes-opera', loadComponent: () => import('./features/acompanantes/acompanantes').then(m => m.AcompanantesComponent), canActivate: [authGuard], data: { permission: P.AccompanyingGuests } },
  { path: 'usuarios', loadComponent: () => import('./features/usuarios/usuarios').then(m => m.UsuariosComponent), canActivate: [authGuard], data: { permission: P.UserAdministration } },
  { path: 'admin', loadComponent: () => import('./features/admin/admin').then(m => m.AdminComponent), canActivate: [authGuard], data: { permission: P.UserAdministration } },
  { path: 'captura-identidad', loadComponent: () => import('./features/ocr/captura-identidad').then(m => m.CapturaIdentidadComponent), canActivate: [authGuard], data: { permission: P.RegistrationCard } },
  { path: 'huesped', loadComponent: () => import('./features/huesped/huesped').then(m => m.HuespedComponent), canActivate: [authGuard] },
  { path: 'huesped/:confirmation', loadComponent: () => import('./features/huesped/huesped').then(m => m.HuespedComponent), canActivate: [authGuard] },
  { path: 'bienvenida', loadComponent: () => import('./features/huesped/huesped').then(m => m.HuespedComponent), canActivate: [authGuard] },
  { path: '**', loadComponent: () => import('./features/not-found/not-found').then(m => m.NotFoundComponent) },
];
