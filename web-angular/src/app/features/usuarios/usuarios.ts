import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AccessAdministration, AppUserInfo, UserGroupInfo } from '../../core/models';

@Component({
  selector: 'app-usuarios',
  imports: [FormsModule],
  template: `
  <header class="mb-4"><div class="page-kicker">Seguridad local</div><h1 class="page-heading">Usuarios y grupos</h1><p class="page-subtitle">Asigne credenciales, grupo y las vistas disponibles para cada equipo.</p></header>
  @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError">{{ message }}</div> }
  @if (!data) { <div class="text-center py-5"><div class="spinner-border text-primary"></div></div> }
  @else {
  <div class="row g-4">
    <section class="col-lg-6"><div class="panel"><div class="d-flex justify-content-between"><h2 class="section-title">Grupos</h2><button class="btn btn-outline-primary" (click)="newGroup()">Nuevo grupo</button></div>
      <div class="list-group my-3">
        @for (g of data.groups; track g.id) {
          <button class="list-group-item list-group-item-action d-flex justify-content-between" (click)="editGroup(g)"><span><strong>{{ g.name }}</strong><br><small>{{ g.description }}</small></span><span class="badge" [class.text-bg-success]="g.isActive" [class.text-bg-secondary]="!g.isActive">{{ g.isActive ? 'Activo' : 'Inactivo' }}</span></button>
        }
      </div>
      @if (group) {
        <div class="border rounded p-3"><h3 class="h6">{{ groupId ? 'Editar grupo' : 'Nuevo grupo' }}</h3>
          <label class="form-label">Nombre</label><input class="form-control mb-2" [(ngModel)]="group.name">
          <label class="form-label">Descripción</label><input class="form-control mb-3" [(ngModel)]="group.description">
          <div class="fw-semibold mb-2">Vistas permitidas</div>
          @for (p of data.permissions; track p.key) {
            <div class="form-check"><input class="form-check-input" type="checkbox" [checked]="group.permissions.includes(p.key)" (change)="togglePerm(p.key, $any($event.target).checked)" [id]="'perm-' + p.key"><label class="form-check-label" [for]="'perm-' + p.key">{{ p.label }}</label></div>
          }
          <div class="form-check mt-3"><input class="form-check-input" type="checkbox" [(ngModel)]="group.isActive" id="ga"><label class="form-check-label" for="ga">Grupo activo</label></div>
          <button class="btn btn-primary mt-3" [disabled]="working" (click)="saveGroup()">Guardar grupo</button>
        </div>
      }
    </div></section>
    <section class="col-lg-6"><div class="panel"><div class="d-flex justify-content-between"><h2 class="section-title">Usuarios</h2><button class="btn btn-outline-primary" (click)="newUser()">Nuevo usuario</button></div>
      <div class="list-group my-3">
        @for (u of data.users; track u.id) {
          <button class="list-group-item list-group-item-action d-flex justify-content-between" (click)="editUser(u)"><span><strong>{{ u.username }}</strong> · {{ u.displayName }}<br><small>{{ groupName(u.userGroupId) }} · {{ u.role }}</small></span><span class="badge" [class.text-bg-success]="u.isActive" [class.text-bg-secondary]="!u.isActive">{{ u.isActive ? 'Activo' : 'Inactivo' }}</span></button>
        }
      </div>
      @if (user) {
        <div class="border rounded p-3"><h3 class="h6">{{ userId ? 'Editar usuario' : 'Nuevo usuario' }}</h3>
          <div class="row g-2">
            <div class="col-md-6"><label class="form-label">Usuario</label><input class="form-control" autocomplete="off" [(ngModel)]="user.username"></div>
            <div class="col-md-6"><label class="form-label">Nombre visible</label><input class="form-control" [(ngModel)]="user.displayName"></div>
            <div class="col-md-6"><label class="form-label">Grupo</label><select class="form-select" [(ngModel)]="user.userGroupId">@for (g of data.groups; track g.id) { @if (g.isActive) { <option [value]="g.id">{{ g.name }}</option> } }</select></div>
            <div class="col-md-6"><label class="form-label">Rol técnico</label><select class="form-select" [(ngModel)]="user.role"><option value="Receptionist">Usuario</option><option value="Admin">Administrador</option></select></div>
            <div class="col-12"><label class="form-label">{{ userId ? 'Nueva contraseña (vacío conserva la actual)' : 'Contraseña' }}</label><input type="password" class="form-control" autocomplete="new-password" [(ngModel)]="user.password"></div>
          </div>
          <div class="form-check mt-3"><input class="form-check-input" type="checkbox" [(ngModel)]="user.isActive" id="ua"><label class="form-check-label" for="ua">Usuario activo</label></div>
          <button class="btn btn-primary mt-3" [disabled]="working" (click)="saveUser()">Guardar usuario</button>
        </div>
      }
    </div></section>
  </div>
  }`
})
export class UsuariosComponent implements OnInit {
  private api = inject(ApiService);
  data: AccessAdministration | null = null;
  group: any = null; user: any = null;
  groupId: number | null = null; userId: number | null = null;
  working = false; isError = false; message = '';

  ngOnInit() { this.reload().catch(e => this.show(e?.error?.message || e.message, true)); }
  reload() { return this.api.getAccessAdministration().then(d => this.data = d); }
  newGroup() { this.groupId = null; this.group = { name: '', description: '', permissions: [], isActive: true }; }
  editGroup(g: UserGroupInfo) { this.groupId = g.id; this.group = { name: g.name, description: g.description, permissions: [...g.permissions], isActive: g.isActive }; }
  togglePerm(k: string, on: boolean) {
    if (on && !this.group.permissions.includes(k)) this.group.permissions.push(k);
    if (!on) this.group.permissions = this.group.permissions.filter((x: string) => x !== k);
  }
  saveGroup() {
    this.working = true;
    this.api.saveUserGroup(this.groupId, this.group).then(
      () => this.reload().then(() => { this.group = null; this.show('Grupo guardado.', false); this.working = false; }),
      e => { this.show(e?.error?.message || e.message, true); this.working = false; }
    );
  }
  newUser() { this.userId = null; this.user = { username: '', displayName: '', role: 'Receptionist', userGroupId: this.data?.groups.find(g => g.isActive)?.id || 0, password: '', isActive: true }; }
  editUser(u: AppUserInfo) { this.userId = u.id; this.user = { username: u.username, displayName: u.displayName, role: u.role, userGroupId: u.userGroupId || 0, password: '', isActive: u.isActive }; }
  saveUser() {
    this.working = true;
    this.api.saveAppUser(this.userId, this.user).then(
      () => this.reload().then(() => { this.user = null; this.show('Usuario guardado. Los permisos se aplicarán en su próximo inicio de sesión.', false); this.working = false; }),
      e => { this.show(e?.error?.message || e.message, true); this.working = false; }
    );
  }
  groupName(id?: number) { return this.data?.groups.find(g => g.id === id)?.name || 'Sin grupo'; }
  show(t: string, err: boolean) { this.message = t; this.isError = err; }
}
