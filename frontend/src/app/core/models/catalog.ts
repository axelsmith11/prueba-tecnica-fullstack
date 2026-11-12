export interface Department {
  departmentCode: string;
  departmentName: string;
}

export interface State {
  stateCode: string;
  stateDescription: string;
}

export interface BonusType {
  bonusTypeId: number;
  bonusTypeDescription: string;
}

export interface BonusReason {
  reasonCode: string;
  reasonDescription: string;
}

export const MasterStateCodes = {
  Employee: '01',
  Bonus: '02',
} as const;

export const BonusStates = {
  Registered: '0201',
  Paid: '0202',
  Cancelled: '0203',
} as const;

export const BonusTypes = {
  FixedAmount: 1,
  SalaryPercentage: 2,
} as const;
