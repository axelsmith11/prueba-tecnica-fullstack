import { BonusFilter, EMPTY_BONUS_FILTER } from '../models/bonus';

const PERIOD_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;
const POSITIVE_INTEGER_PATTERN = /^[1-9]\d*$/;

/** Lee el filtro de los parámetros de la URL (area, empleado, periodo, estado); ignora los valores inválidos. */
export function filterFromQueryParams(params: { get(name: string): string | null }): BonusFilter {
  const employee = params.get('empleado')?.trim() ?? '';
  const period = params.get('periodo')?.trim() ?? '';
  const employeeNumber = POSITIVE_INTEGER_PATTERN.test(employee) ? Number(employee) : null;
  return {
    ...EMPTY_BONUS_FILTER,
    departmentCode: params.get('area')?.trim() ?? '',
    employeeNumber: Number.isSafeInteger(employeeNumber) ? employeeNumber : null,
    period: PERIOD_PATTERN.test(period) ? period : '',
    bonusStateCode: params.get('estado')?.trim() ?? '',
  };
}

/** Convierte el filtro en parámetros de URL, omitiendo los vacíos. */
export function filterToQueryParams(filter: BonusFilter): Record<string, string> {
  const params: Record<string, string> = {};
  if (filter.departmentCode) params['area'] = filter.departmentCode;
  if (filter.employeeNumber !== null) params['empleado'] = String(filter.employeeNumber);
  if (filter.period) params['periodo'] = filter.period;
  if (filter.bonusStateCode) params['estado'] = filter.bonusStateCode;
  return params;
}
