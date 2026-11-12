export interface PeriodOption {
  /** Valor que se envía a la API (YYYY-MM). */
  value: string;
  /** Texto que ve el usuario, p. ej. "Noviembre 2026". */
  label: string;
}

const MONTH_NAMES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
];

/** Cantidad de meses que ofrecen los selectores de periodo (3 años hacia atrás). */
export const PERIOD_OPTIONS_COUNT = 36;

/** Mes actual en formato YYYY-MM. */
export function currentPeriod(today: Date = new Date()): string {
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
}

/**
 * Periodos para los desplegables: del mes actual hacia atrás, sin meses futuros
 * (un bono no puede registrarse para un periodo posterior al mes actual).
 */
export function recentPeriods(count: number = PERIOD_OPTIONS_COUNT, today: Date = new Date()): PeriodOption[] {
  return Array.from({ length: count }, (_, monthsAgo) => {
    const month = new Date(today.getFullYear(), today.getMonth() - monthsAgo, 1);
    return { value: currentPeriod(month), label: `${MONTH_NAMES[month.getMonth()]} ${month.getFullYear()}` };
  });
}
