import { DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, of } from 'rxjs';
import { BonusApi } from '../../../core/services/bonus-api';

export interface PaymentDetailDialogData {
  bonusId: number;
  fullName: string;
  period: string;
}

/** Reparto del bono pagado entre los proyectos del empleado (para imputación contable). */
@Component({
  selector: 'app-payment-detail-dialog',
  imports: [DecimalPipe, MatButtonModule, MatDialogModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Detalle del pago</h2>
    <mat-dialog-content>
      <p class="subtitle">{{ data.fullName }} · {{ data.period }}</p>
      @if (detail() === undefined) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando detalle del pago" />
      } @else if (detail(); as paid) {
        <table class="detail">
          <thead>
            <tr>
              <th scope="col">Proyecto</th>
              <th scope="col" class="amount">Horas</th>
              <th scope="col" class="amount">Monto</th>
            </tr>
          </thead>
          <tbody>
            @for (line of paid.lines; track $index) {
              <tr>
                <td>{{ line.projectName }}</td>
                <td class="amount">{{ line.hours | number: '1.2-2' }}</td>
                <td class="amount">{{ line.amount | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr>
              <th scope="row">Total</th>
              <td class="amount">{{ totalHours() | number: '1.2-2' }}</td>
              <td class="amount">{{ paid.grossAmount | number: '1.2-2' }}</td>
            </tr>
          </tfoot>
        </table>
      } @else {
        <p class="error">No se pudo cargar el detalle del pago.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton="filled" type="button" mat-dialog-close>Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .subtitle { margin: 0 0 12px; color: var(--text-muted); }
    .detail { width: 100%; border-collapse: collapse; }
    .detail th, .detail td { padding: 8px 4px; border-bottom: 1px solid var(--border); text-align: left; }
    .detail .amount { text-align: right; font-variant-numeric: tabular-nums; }
    .detail tfoot th, .detail tfoot td { font-weight: 600; border-bottom: 0; }
    .error { color: var(--text-muted); }
  `,
})
export class PaymentDetailDialog {
  protected readonly data = inject<PaymentDetailDialogData>(MAT_DIALOG_DATA);
  // undefined = cargando; null = falló (el interceptor ya mostró el motivo).
  protected readonly detail = toSignal(
    inject(BonusApi).paymentDetail(this.data.bonusId).pipe(catchError(() => of(null))),
    { initialValue: undefined },
  );
  protected readonly totalHours = computed(() =>
    (this.detail()?.lines ?? []).reduce((sum, line) => sum + line.hours, 0),
  );
}
