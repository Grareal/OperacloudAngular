import { Component, ViewChild, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { OfficialCardInput, OfficialCardOccupantInput, Reservation } from '../../core/models';
import { SignatureCanvasComponent } from '../../shared/signature-canvas';

@Component({
  selector: 'app-tarjeta',
  imports: [FormsModule, RouterLink, SignatureCanvasComponent],
  template: `
  <div class="d-flex flex-wrap justify-content-between align-items-start gap-3 mb-4">
    <div>
      <div class="page-kicker">Check-in sin papel</div>
      <h1 class="page-heading">Registration Card digital</h1>
      <p class="page-subtitle">Reserva {{ confirmation }} · PDF oficial de OPERA Cloud</p>
    </div>
    <a class="btn btn-outline-secondary" routerLink="/">Volver al dashboard</a>
  </div>
  @if (loading) {
    <div class="panel empty-state" role="status"><div class="spinner-border text-primary mb-3"></div><div>Consultando la reservación en OPERA…</div></div>
  } @else if (!reservation) {
    <div class="alert alert-warning">No se encontró la reservación {{ confirmation }}.</div>
  } @else {
    <ol class="workflow-steps" aria-label="Progreso de la tarjeta">
      @for (s of steps; track s.n) {
        <li [class.active]="s.n === step" [class.complete]="s.n < step"><span>{{ s.n < step ? '✓' : s.n }}</span>{{ s.label }}</li>
      }
    </ol>
    @if (message) { <div class="alert" [class.alert-danger]="isError" [class.alert-success]="!isError" role="alert">{{ message }}</div> }
    <div class="row g-4">
      <aside class="col-lg-4">
        <div class="panel reservation-summary">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-3">
            <div><div class="small text-secondary">Huésped principal</div><h2 class="section-title mt-1">{{ reservation.guest.fullName }}</h2></div>
            <span class="badge text-bg-success">{{ reservation.reservationStatus }}</span>
          </div>
          <dl class="summary-list">
            <dt>Habitación</dt><dd>{{ dash(reservation.roomStay.roomId) }}</dd>
            <dt>Room type</dt><dd>{{ dash(reservation.roomStay.roomType) }}</dd>
            <dt>Llegada</dt><dd>{{ dash(reservation.roomStay.arrivalDate) }}</dd>
            <dt>Salida</dt><dd>{{ dash(reservation.roomStay.departureDate) }}</dd>
            <dt>Huéspedes</dt><dd>{{ reservation.roomStay.adultCount }} adultos · {{ reservation.roomStay.childCount }} menores</dd>
          </dl>
          <div class="template-note mt-3">La plantilla se seleccionará automáticamente a partir del room type.</div>
        </div>
      </aside>
      <section class="col-lg-8">
        <div class="panel workflow-panel">
          @if (step === 1) {
            <h2 class="section-title">Datos del huésped</h2>
            <p class="text-secondary">Confirma la información que aparecerá en el documento. Esto no actualiza el perfil de OPERA.</p>
            <div class="row g-3 mt-1">
              <div class="col-md-6"><label class="form-label">Correo electrónico</label><input class="form-control" type="email" [(ngModel)]="input.email"><div class="form-text">Enviaremos aquí la tarjeta firmada y los documentos vigentes de su estancia.</div></div>
              <div class="col-md-6"><label class="form-label">Teléfono celular</label><input class="form-control" [(ngModel)]="input.cellPhone"></div>
              <div class="col-md-6"><label class="form-label">Nacionalidad</label><input class="form-control" [(ngModel)]="input.citizenship"></div>
              <div class="col-md-6"><label class="form-label">Ciudad</label><input class="form-control" [(ngModel)]="input.city"></div>
              <div class="col-md-6"><label class="form-label">Estado</label><input class="form-control" [(ngModel)]="input.state"></div>
              <div class="col-md-6"><label class="form-label">País</label><input class="form-control" [(ngModel)]="input.country"></div>
            </div>
            <div class="workflow-actions"><span></span><button class="btn btn-primary" (click)="step = 2">Continuar con ocupantes</button></div>
          } @else if (step === 2) {
            <div class="d-flex justify-content-between gap-3 align-items-start">
              <div><h2 class="section-title">Ocupantes autorizados</h2><p class="text-secondary">Agrega únicamente adultos que firmarán la autorización de cargos.</p></div>
              <span class="capacity-pill">{{ input.occupants.length }} / 8</span>
            </div>
            <div class="occupant-list mt-3">
              @for (o of input.occupants; track $index) {
                <div class="occupant-row">
                  <span class="occupant-number">{{ $index + 1 }}</span>
                  <div class="form-check occupant-select"><input class="form-check-input" type="checkbox" [(ngModel)]="o.selected"></div>
                  <div class="flex-grow-1"><input class="form-control" placeholder="Nombre completo" [(ngModel)]="o.name"></div>
                  <button type="button" class="btn btn-outline-danger" (click)="removeOccupant($index)">Quitar</button>
                </div>
              }
            </div>
            <div class="border rounded p-3 mt-4">
              <h3 class="h6">Agregar un adulto a la reserva de OPERA</h3>
              <p class="small text-secondary">Buscaremos primero si el huésped ya tiene perfil.</p>
              <div class="row g-2">
                <div class="col-md-5"><label class="form-label">Nombre(s)</label><input class="form-control" maxlength="40" [(ngModel)]="operaGiven"></div>
                <div class="col-md-5"><label class="form-label">Apellido(s)</label><input class="form-control" maxlength="40" [(ngModel)]="operaSurname"></div>
                <div class="col-md-2 d-flex align-items-end"><button class="btn btn-outline-primary w-100" [disabled]="operaWorking" (click)="lookupGuest()">Buscar</button></div>
              </div>
              @if (lookup?.matches?.length) {
                <div class="alert alert-info mt-3 mb-0"><strong>OPERA encontró perfiles existentes.</strong>
                  <div class="list-group mt-2">
                    @for (m of lookup.matches; track m.profileId) {
                      <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center" [disabled]="operaWorking" (click)="previewGuest(m.profileId)">
                        <span>{{ m.fullName }}</span><span class="badge text-bg-light">Profile {{ m.profileId }}</span>
                      </button>
                    }
                  </div>
                </div>
              }
              @if (lookup?.newProfilePreview) {
                <div class="alert alert-warning mt-3 mb-0"><strong>No se encontró un perfil existente.</strong><br>
                  Se creará <strong>{{ lookup.newProfilePreview.requestedProfile.fullName }}</strong>. Adultos: {{ lookup.newProfilePreview.currentAdults }} → {{ lookup.newProfilePreview.proposedAdults }}.
                  @if (lookup.newProfilePreview.canApply) {
                    <div class="form-check mt-2"><input class="form-check-input" type="checkbox" [(ngModel)]="confirmOpera" id="cnew"><label class="form-check-label" for="cnew">Confirmo el nombre y que este adulto pertenece a la reserva.</label></div>
                    <button class="btn btn-danger mt-2" [disabled]="!confirmOpera || operaWorking" (click)="createGuest()">Crear Profile ID y vincular</button>
                  }
                </div>
              }
              @if (guestPreview) {
                <div class="alert alert-info mt-3 mb-0"><strong>{{ guestPreview.requestedProfile.fullName }}</strong> · Profile {{ guestPreview.requestedProfile.profileId }}<br>
                  Adultos: {{ guestPreview.currentAdults }} → {{ guestPreview.proposedAdults }}. {{ guestPreview.validationMessage }}
                  @if (guestPreview.canApply) {
                    <div class="form-check mt-2"><input class="form-check-input" type="checkbox" [(ngModel)]="confirmOpera" id="cex"><label class="form-check-label" for="cex">Confirmo que este adulto pertenece a la reserva.</label></div>
                    <button class="btn btn-danger mt-2" [disabled]="!confirmOpera || operaWorking" (click)="applyGuest()">Vincular adulto en OPERA</button>
                  }
                </div>
              }
            </div>
            @if (input.occupants.length < 8) { <button type="button" class="btn btn-outline-primary mt-3" (click)="addOccupant()">+ Agregar ocupante</button> }
            <div class="workflow-actions"><button class="btn btn-outline-secondary" (click)="step = 1">Atrás</button><button class="btn btn-primary" (click)="startSignatures()">Continuar con firmas</button></div>
          } @else if (step === 3) {
            <div class="signature-progress">Firma {{ sigIndex + 1 }} de {{ totalSigners }}</div>
            <h2 class="section-title mt-2">{{ currentSigner }}</h2>
            <p class="text-secondary">Firma dentro del recuadro usando el dedo o lápiz digital.</p>
            <app-signature-canvas #pad />
            <div class="d-flex justify-content-between align-items-center gap-3 mt-3">
              <button class="btn btn-outline-secondary" (click)="pad.clear()">Limpiar</button>
              <button class="btn btn-primary" (click)="capture()">Confirmar firma</button>
            </div>
            <button class="btn btn-link px-0 mt-3" (click)="step = 2; sigIndex = 0">Volver a ocupantes</button>
          } @else {
            <h2 class="section-title">Revisión y envío</h2>
            <p class="text-secondary">Genera el PDF final para revisarlo antes de crear el adjunto en OPERA.</p>
            <div class="review-grid">
              <div><span>Firmas capturadas</span><strong>{{ totalSigners }}</strong></div>
              <div><span>Correo</span><strong>{{ dash(input.email) }}</strong></div>
              <div class="form-check mt-3"><input id="mc" class="form-check-input" type="checkbox" [(ngModel)]="input.marketingConsent"><label class="form-check-label" for="mc">Acepto recibir promociones y beneficios de Vidanta junto con mis documentos.</label></div>
              <div><span>Celular</span><strong>{{ dash(input.cellPhone) }}</strong></div>
            </div>
            <div class="d-grid d-md-flex gap-3 mt-4">
              <button class="btn btn-outline-primary" [disabled]="working" (click)="preview()">{{ working ? 'Generando…' : 'Descargar vista previa' }}</button>
              <button class="btn btn-primary" [disabled]="!previewReady || !confirmSend || working" (click)="upload()">Enviar a OPERA</button>
            </div>
            <div class="form-check mt-4"><input id="cs" class="form-check-input" type="checkbox" [(ngModel)]="confirmSend"><label class="form-check-label" for="cs">Revisé el documento y confirmo que las firmas y datos son correctos.</label></div>
            @if (!previewReady) { <div class="small text-secondary mt-2">Primero descarga y revisa la vista previa.</div> }
            <div class="workflow-actions"><button class="btn btn-outline-secondary" (click)="step = 3; sigIndex = 0; previewReady = false; confirmSend = false">Volver a firmar</button><span></span></div>
          }
        </div>
      </section>
    </div>
  }`
})
export class TarjetaComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  @ViewChild('pad') pad?: SignatureCanvasComponent;
  confirmation = '';
  steps = [{ n: 1, label: 'Datos' }, { n: 2, label: 'Ocupantes' }, { n: 3, label: 'Firmas' }, { n: 4, label: 'Revisión' }];
  step = 1; sigIndex = 0;
  reservation: Reservation | null = null;
  input: OfficialCardInput = { marketingConsent: false, occupants: [] };
  loading = true; working = false; previewReady = false; confirmSend = false;
  message = ''; isError = false;
  operaGiven = ''; operaSurname = ''; operaWorking = false; confirmOpera = false;
  lookup: any = null; guestPreview: any = null; operaProfileId = '';

  ngOnInit() {
    this.confirmation = this.route.snapshot.params['confirmation'];
    this.api.getReservation(this.confirmation).then(
      list => {
        const r = list[0];
        if (r) {
          this.reservation = r;
          this.input.primaryGuestName = r.guest.fullName;
          this.input.primarySignerId = r.guest.id;
          this.input.email = r.guest.email; this.input.cellPhone = r.guest.phoneNumber;
          this.input.city = r.guest.address?.city; this.input.state = r.guest.address?.stateProvCode; this.input.country = r.guest.address?.countryCode;
          for (const g of (r.accompanyingGuests || []).slice(0, 8))
            this.input.occupants.push({ name: g.fullName, signerId: g.profileId ?? g.reservationGuestId, signaturePngBase64: '', selected: true });
          if (!this.input.occupants.length)
            for (const n of (r.accompanyingGuestNames || []).slice(0, 8))
              this.input.occupants.push({ name: n, signaturePngBase64: '', selected: true });
          const expected = Math.min(8, Math.max(0, r.roomStay.adultCount - 1));
          while (this.input.occupants.length < expected)
            this.input.occupants.push({ name: '', signaturePngBase64: '', selected: true });
        }
        this.loading = false;
      },
      e => { this.setMsg(e?.error?.message || e.message, true); this.loading = false; }
    );
  }

  get totalSigners() { return this.input.occupants.length + 1; }
  get signingPrimary() { return this.sigIndex >= this.input.occupants.length; }
  get currentSigner() { return this.signingPrimary ? (this.input.primaryGuestName || 'Huésped principal') : this.input.occupants[this.sigIndex].name; }

  addOccupant() { if (this.input.occupants.length < 8) this.input.occupants.push({ name: '', signaturePngBase64: '', selected: true } as OfficialCardOccupantInput); }
  removeOccupant(i: number) { this.input.occupants.splice(i, 1); }
  startSignatures() {
    const sel = this.input.occupants.filter(o => o.selected);
    if (sel.some(o => !o.name.trim())) { this.setMsg('Capture el nombre completo de cada ocupante seleccionado o quite su selección.', true); return; }
    this.input.occupants = sel; this.msg(''); this.sigIndex = 0; this.step = 3; this.previewReady = false;
  }
  capture() {
    const png = this.pad?.getPng();
    if (!png) { this.setMsg('Capture la firma antes de continuar.', true); return; }
    if (this.signingPrimary) this.input.primarySignaturePngBase64 = png;
    else this.input.occupants[this.sigIndex].signaturePngBase64 = png;
    this.msg('');
    if (this.sigIndex + 1 >= this.totalSigners) { this.step = 4; this.previewReady = false; return; }
    this.sigIndex++; this.pad?.clear();
  }
  preview() {
    this.working = true; this.msg('');
    this.api.previewOfficialCard(this.confirmation, this.input).then(
      b => { this.api.downloadBlob(b, `REGCARD${this.confirmation}PREVIEW.pdf`); this.previewReady = true; this.setMsg('Vista previa generada. Revísala antes de enviar.', false); this.working = false; },
      e => { this.setMsg(e?.error || e.message, true); this.working = false; }
    );
  }
  upload() {
    this.working = true; this.msg('');
    this.api.uploadOfficialCard(this.confirmation, this.input).then(
      r => {
        const mail = r.emailStatus === 'Pending' ? ' El correo quedó en cola para envío SMTP.' : r.emailStatus === 'SkippedNoEmail' ? ' No se encoló correo: sin destinatario válido.' : r.emailStatus === 'QueueFailed' ? ' No fue posible encolar el correo.' : '';
        this.setMsg(`Documento enviado a OPERA. Attachment ID: ${r.attachmentId}.${mail}`, r.emailStatus === 'QueueFailed');
        this.confirmSend = false; this.working = false;
      },
      e => { this.setMsg(typeof e?.error === 'string' ? e.error : e.message, true); this.working = false; }
    );
  }
  lookupGuest() {
    if (!this.operaGiven.trim() || !this.operaSurname.trim()) { this.setMsg('Capture nombre y apellido del acompañante.', true); return; }
    this.operaWorking = true; this.lookup = null; this.guestPreview = null; this.confirmOpera = false;
    this.api.lookupAccompanyingAdult(this.confirmation, this.operaGiven, this.operaSurname).then(
      l => { this.lookup = l; this.operaWorking = false; },
      e => { this.setMsg(e?.error?.message || e.message, true); this.operaWorking = false; }
    );
  }
  previewGuest(pid: string) {
    this.operaProfileId = pid; this.guestPreview = null; this.confirmOpera = false; this.operaWorking = true;
    this.api.previewAddAccompanyingAdult(this.confirmation, pid).then(
      p => { this.guestPreview = p; this.operaWorking = false; },
      e => { this.setMsg(e?.error?.message || e.message, true); this.operaWorking = false; }
    );
  }
  applyGuest() {
    if (!this.guestPreview?.canApply || !this.confirmOpera) return;
    this.operaWorking = true;
    this.api.addAccompanyingAdult(this.confirmation, this.operaProfileId, this.guestPreview.lastModifyDateTime, 'AGREGAR ACOMPAÑANTE').then(
      r => {
        const g = r.after.requestedProfile;
        if (!this.input.occupants.some(o => o.signerId === g.profileId))
          this.input.occupants.push({ name: g.fullName, signerId: g.profileId, signaturePngBase64: '', selected: true });
        if (this.reservation) this.reservation.roomStay.adultCount = r.after.currentAdults;
        this.guestPreview = null; this.confirmOpera = false; this.operaWorking = false;
        this.setMsg(`${g.fullName} fue agregado y verificado en OPERA. Ya puede firmar.`, false);
      },
      e => { this.setMsg(e?.error?.message || e.message, true); this.operaWorking = false; }
    );
  }
  createGuest() {
    const pv = this.lookup?.newProfilePreview;
    if (!pv?.canApply || !this.confirmOpera) return;
    this.operaWorking = true;
    this.api.createAndAddAccompanyingAdult(this.confirmation, this.operaGiven, this.operaSurname, pv.lastModifyDateTime, 'AGREGAR ACOMPAÑANTE').then(
      r => {
        const g = r.after.requestedProfile;
        if (!this.input.occupants.some(o => o.signerId === g.profileId))
          this.input.occupants.push({ name: g.fullName, signerId: g.profileId, signaturePngBase64: '', selected: true });
        if (this.reservation) this.reservation.roomStay.adultCount = r.after.currentAdults;
        this.lookup = null; this.operaGiven = ''; this.operaSurname = ''; this.confirmOpera = false; this.operaWorking = false;
        this.setMsg(`Profile ID ${g.profileId} creado para ${g.fullName} y vinculado en OPERA.`, false);
      },
      e => { this.setMsg(e?.error?.message || e.message, true); this.operaWorking = false; }
    );
  }
  dash(v?: string) { return v?.trim() ? v : '—'; }
  private msg(t: string) { this.message = t; this.isError = false; }
  private setMsg(t: string, err: boolean) { this.message = t; this.isError = err; }
}
