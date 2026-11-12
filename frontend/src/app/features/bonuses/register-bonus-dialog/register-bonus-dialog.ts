import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { EMPTY, Subject, catchError, distinctUntilChanged, finalize, map, noop, switchMap, takeUntil } from 'rxjs';
import { EmployeeLookup } from '../../../core/models/bonus';
import { BonusTypes } from '../../../core/models/catalog';
import { BonusApi } from '../../../core/services/bonus-api';
import { CatalogApi } from '../../../core/services/catalog-api';
import { calculateBonus } from '../../../core/utils/bonus-calculator';
import { currentPeriod, recentPeriods } from '../../../core/utils/period';

const TWO_DECIMALS_PATTERN = /^\d+(\.\d{1,2})?$/;
const MAX_PERCENTAGE = 100;

/** Modal para buscar un empleado activo y registrarle un bono con vista previa del cálculo. */
@Component({
  selector: 'app-register-bonus-dialog',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSelectModule,
  ],
  templateUrl: './register-bonus-dialog.html',
  styleUrl: './register-bonus-dialog.css',
})
export class RegisterBonusDialog {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly dialogRef = inject<MatDialogRef<RegisterBonusDialog, boolean>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  private readonly search$ = new Subject<{ departmentCode: string; employeeNumber: number }>();
  private readonly cancelSearch$ = new Subject<void>();

  // El desplegable solo ofrece meses hasta el actual: un periodo futuro no se puede elegir.
  protected readonly periodOptions = recentPeriods();
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusTypes = toSignal(this.catalogApi.getBonusTypes(), { initialValue: [] });
  protected readonly reasons = toSignal(this.catalogApi.getBonusReasons(), { initialValue: [] });
  protected readonly employee = signal<EmployeeLookup | null>(null);
  protected readonly searching = signal(false);
  protected readonly saving = signal(false);

  protected readonly searchForm = this.fb.group({
    departmentCode: ['', Validators.required],
    employeeNumber: [null as number | null, [Validators.required, Validators.min(1)]],
  });

  protected readonly bonusForm = this.fb.group({
    period: [currentPeriod(), Validators.required],
    bonusTypeId: [BonusTypes.SalaryPercentage as number, Validators.required],
    bonusValue: [null as number | null, [Validators.required, (control: AbstractControl) => this.validateBonusValue(control)]],
    reasonCode: ['', Validators.required],
  });

  private readonly formValue = toSignal(
    this.bonusForm.valueChanges.pipe(map(() => this.bonusForm.getRawValue())),
    { initialValue: this.bonusForm.getRawValue() },
  );

  protected readonly isPercentage = computed(() => this.formValue().bonusTypeId === BonusTypes.SalaryPercentage);
  protected readonly valueLabel = computed(() => (this.isPercentage() ? 'Porcentaje del sueldo' : 'Monto fijo'));
  protected readonly maxBonusValue = computed(() =>
    this.isPercentage() ? MAX_PERCENTAGE : (this.employee()?.monthlySalary ?? 0),
  );
  protected readonly preview = computed(() => {
    const employee = this.employee();
    const { bonusTypeId, bonusValue } = this.formValue();
    if (!employee || bonusValue === null || bonusValue <= 0) {
      return { gross: 0, tax: 0, net: 0 };
    }
    return calculateBonus(bonusTypeId, bonusValue, employee.monthlySalary, employee.taxRate);
  });

  constructor() {
    // Cambiar el tipo cambia el máximo permitido: se revalida el valor.
    // Se escucha el grupo (no el control de tipo) porque su valueChanges se emite antes de
    // que formValue (y, con él, maxBonusValue) tenga el tipo nuevo; aquí ya lo tiene.
    this.bonusForm.valueChanges
      .pipe(
        map(() => this.bonusForm.controls.bonusTypeId.value),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.bonusForm.controls.bonusValue.updateValueAndValidity());
    // Si se cambia el área o el número, el empleado mostrado ya no corresponde.
    this.searchForm.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.cancelSearch$.next();
      this.employee.set(null);
    });
    // switchMap descarta la respuesta de una búsqueda anterior; cancelSearch$ cancela la pendiente
    // cuando cambian los campos de búsqueda.
    this.search$
      .pipe(
        switchMap(({ departmentCode, employeeNumber }) => {
          this.searching.set(true);
          return this.bonusApi.getEmployee(departmentCode, employeeNumber).pipe(
            takeUntil(this.cancelSearch$),
            catchError(() => EMPTY), // el interceptor ya mostró el motivo (no existe o está cesado)
            finalize(() => this.searching.set(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((employee) => {
        this.employee.set(employee);
        this.bonusForm.controls.bonusValue.updateValueAndValidity();
      });
  }

  protected searchEmployee(): void {
    if (this.searchForm.invalid) {
      this.searchForm.markAllAsTouched();
      return;
    }
    const { departmentCode, employeeNumber } = this.searchForm.getRawValue();
    this.search$.next({ departmentCode, employeeNumber: employeeNumber! });
  }

  protected save(): void {
    const employee = this.employee();
    if (!employee || this.bonusForm.invalid) {
      this.bonusForm.markAllAsTouched();
      return;
    }
    const { period, bonusTypeId, bonusValue, reasonCode } = this.bonusForm.getRawValue();
    this.saving.set(true);
    this.bonusApi
      .register({
        departmentCode: employee.departmentCode,
        employeeNumber: employee.employeeNumber,
        period,
        bonusTypeId,
        bonusValue: bonusValue!,
        reasonCode,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({ next: () => this.dialogRef.close(true), error: noop });
  }

  protected bonusValueError(): string {
    const errors = this.bonusForm.controls.bonusValue.errors;
    if (errors?.['required']) return 'Ingresa un valor';
    if (errors?.['min']) return 'Debe ser mayor a 0';
    if (errors?.['max']) return `No puede superar ${errors['max'].max}`;
    if (errors?.['decimals']) return 'Usa hasta 2 decimales';
    return '';
  }

  private validateBonusValue(control: AbstractControl): ValidationErrors | null {
    const value = control.value as number | null;
    if (value === null) return null;
    if (value <= 0) return { min: true };
    if (!TWO_DECIMALS_PATTERN.test(String(value))) return { decimals: true };
    const max = this.maxBonusValue();
    return this.employee() && value > max ? { max: { max } } : null;
  }
}
