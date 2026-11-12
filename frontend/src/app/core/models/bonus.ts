export interface BonusFilter {
  departmentCode: string;
  employeeNumber: number | null;
  period: string;
  bonusStateCode: string;
}

export const EMPTY_BONUS_FILTER: BonusFilter = {
  departmentCode: '',
  employeeNumber: null,
  period: '',
  bonusStateCode: '',
};

export interface BonusListItem {
  bonusId: number;
  bonusStateCode: string;
  stateDescription: string;
  period: string;
  departmentCode: string;
  departmentName: string;
  employeeNumber: number;
  fullName: string;
  bonusTypeId: number;
  bonusTypeDescription: string;
  bonusValue: number;
  reasonDescription: string;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

export interface EmployeeLookup {
  departmentCode: string;
  employeeNumber: number;
  fullName: string;
  hireDate: string;
  monthlySalary: number;
  taxRate: number;
}

export interface RegisterBonusRequest {
  departmentCode: string;
  employeeNumber: number;
  period: string;
  bonusTypeId: number;
  bonusValue: number;
  reasonCode: string;
}

export interface RegisteredBonus {
  bonusId: number;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

export interface BonusAmounts {
  gross: number;
  tax: number;
  net: number;
}
