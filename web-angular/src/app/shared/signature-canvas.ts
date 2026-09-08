import { Component, ElementRef, ViewChild, AfterViewInit, input, output } from '@angular/core';

/** Canvas de firma táctil → PNG base64 (reemplaza signature-pad.js). */
@Component({
  selector: 'app-signature-canvas',
  template: `<div class="signature-box"><canvas #cv width="720" height="240"></canvas></div>`,
})
export class SignatureCanvasComponent implements AfterViewInit {
  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;
  cleared = output<void>();
  private ctx!: CanvasRenderingContext2D;
  private drawing = false;
  private hasInk = false;

  ngAfterViewInit() {
    const c = this.cv.nativeElement;
    this.ctx = c.getContext('2d')!;
    this.ctx.lineWidth = 2.5; this.ctx.lineCap = 'round';
    this.ctx.strokeStyle = '#003E55';
    const pos = (e: PointerEvent) => {
      const r = c.getBoundingClientRect();
      return { x: (e.clientX - r.left) * (c.width / r.width), y: (e.clientY - r.top) * (c.height / r.height) };
    };
    c.addEventListener('pointerdown', e => { this.drawing = true; const p = pos(e); this.ctx.beginPath(); this.ctx.moveTo(p.x, p.y); c.setPointerCapture(e.pointerId); });
    c.addEventListener('pointermove', e => { if (!this.drawing) return; const p = pos(e); this.ctx.lineTo(p.x, p.y); this.ctx.stroke(); this.hasInk = true; });
    c.addEventListener('pointerup', () => this.drawing = false);
  }

  getPng(): string | null {
    if (!this.hasInk) return null;
    return this.cv.nativeElement.toDataURL('image/png');
  }

  clear() {
    this.ctx.clearRect(0, 0, this.cv.nativeElement.width, this.cv.nativeElement.height);
    this.hasInk = false;
    this.cleared.emit();
  }
}
