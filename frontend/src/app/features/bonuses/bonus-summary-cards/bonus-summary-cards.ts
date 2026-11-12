import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { BonusSummary } from '../../../core/models/bonus';

/** Tarjetas con lo pendiente de pagar, lo pagado y lo comprometido según los filtros aplicados. */
@Component({
  selector: 'app-bonus-summary-cards',
  imports: [DecimalPipe],
  templateUrl: './bonus-summary-cards.html',
  styleUrl: './bonus-summary-cards.css',
})
export class BonusSummaryCards {
  readonly summary = input<BonusSummary | null>(null);
  protected readonly placeholders = [0, 1, 2, 3];
}
