import { ValidatorFn } from '@angular/forms';

export const PERIOD_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

/** Mes actual en formato YYYY-MM. */
export function currentPeriod(today: Date = new Date()): string {
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
}

/** Rechaza periodos posteriores a maxPeriod (la comparación de texto funciona con YYYY-MM). */
export function maxPeriodValidator(maxPeriod: string): ValidatorFn {
  return (control) =>
    typeof control.value === 'string' && control.value > maxPeriod ? { maxPeriod: true } : null;
}
