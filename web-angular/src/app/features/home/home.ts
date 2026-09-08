import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
  <div class="portal-home">
    <section class="portal-hero">
      <div class="portal-hero-copy">
        <span class="portal-eyebrow">EXPERIENCIA VIDANTA</span>
        <h1>Una bienvenida<br>extraordinaria comienza aquí.</h1>
        <p>Registro digital, firma segura y atención personalizada en una sola experiencia conectada con OPERA Cloud.</p>
        <div class="portal-trust"><span><b>✓</b> Datos protegidos</span><span><b>✓</b> Proceso digital</span><span><b>✓</b> Atención ágil</span></div>
      </div>
    </section>
    <section class="portal-actions" aria-labelledby="portal-title">
      <div class="portal-actions-heading">
        <span class="guest-eyebrow">BIENVENIDO</span>
        <h2 id="portal-title">¿Cómo desea continuar?</h2>
        <p>Seleccione el acceso correspondiente para comenzar.</p>
      </div>
      <a class="portal-action portal-action-primary" routerLink="/huesped">
        <span class="portal-action-icon">01</span>
        <span class="portal-action-copy"><strong>Soy huésped</strong><small>Iniciar mi registro con número de reservación</small></span>
        <span class="portal-action-arrow">→</span>
      </a>
      <a class="portal-action" routerLink="/operacion">
        <span class="portal-action-icon">02</span>
        <span class="portal-action-copy"><strong>Operación</strong><small>Localizar estancias y comenzar el proceso de firma</small></span>
        <span class="portal-action-arrow">→</span>
      </a>
      <a class="portal-action" routerLink="/meseros">
        <span class="portal-action-icon">03</span>
        <span class="portal-action-copy"><strong>Validación de firmas</strong><small>Consulta operativa para restaurantes y centros de consumo</small></span>
        <span class="portal-action-arrow">→</span>
      </a>
      <p class="portal-help">Si necesita asistencia, acérquese con uno de nuestros anfitriones.</p>
    </section>
  </div>`
})
export class HomeComponent {}
