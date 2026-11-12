import { BonusAmounts } from '../models/bonus';
import { BonusTypes } from '../models/catalog';

/** Redondea a 2 decimales con el medio hacia arriba. */
export function roundMoney(value: number): number {
  return (Math.sign(value) * Math.round(Number((Math.abs(value) * 100).toPrecision(15)))) / 100;
}

/**
 * Replica la fórmula de la API solo para la vista previa del modal.
 * El monto oficial siempre lo calcula y guarda el backend.
 */
export function calculateBonus(
  bonusTypeId: number,
  bonusValue: number,
  monthlySalary: number,
  taxRate: number,
): BonusAmounts {
  const gross =
    bonusTypeId === BonusTypes.FixedAmount
      ? roundMoney(bonusValue)
      : roundMoney((monthlySalary * bonusValue) / 100);
  const tax = roundMoney((gross * taxRate) / 100);
  return { gross, tax, net: roundMoney(gross - tax) };
}
