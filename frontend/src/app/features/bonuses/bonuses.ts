import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EMPTY, Observable, Subject, catchError, filter, finalize, noop, switchMap } from 'rxjs';
import { BonusFilter, BonusListItem, EMPTY_BONUS_FILTER } from '../../core/models/bonus';
import { BonusStates, BonusTypes, MasterStateCodes } from '../../core/models/catalog';
import { PagedResponse } from '../../core/models/paged-response';
import { BonusApi } from '../../core/services/bonus-api';
import { CatalogApi } from '../../core/services/catalog-api';
import { Notification } from '../../core/services/notification';
import { ConfirmDialogData, confirmAction } from '../../shared/confirm-dialog/confirm-dialog';
import { RegisterBonusDialog } from './register-bonus-dialog/register-bonus-dialog';

/** Pantalla de bonos de desempeño: filtros, listado paginado en servidor y acciones por fila. */
@Component({
  selector: 'app-bonuses',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './bonuses.html',
  styleUrl: './bonuses.css',
})
export class Bonuses {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly dialog = inject(MatDialog);
  private readonly notification = inject(Notification);
  private readonly reload$ = new Subject<void>();
  private appliedFilter: BonusFilter = EMPTY_BONUS_FILTER;

  protected readonly pageSize = 10;
  protected readonly displayedColumns = [
    'actions', 'state', 'period', 'employee', 'fullName', 'type', 'value', 'reason', 'gross', 'tax', 'net',
  ];
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusStates = toSignal(this.catalogApi.getStates(MasterStateCodes.Bonus), { initialValue: [] });
  protected readonly loading = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly page = signal<PagedResponse<BonusListItem> | null>(null);

  protected readonly filterForm = inject(NonNullableFormBuilder).group({
    departmentCode: [''],
    employeeNumber: [null as number | null, Validators.min(1)],
    period: [''],
    bonusStateCode: [''],
  });

  constructor() {
    this.reload$
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          return this.bonusApi.search(this.appliedFilter, this.pageIndex() + 1, this.pageSize).pipe(
            catchError(() => EMPTY), // el interceptor ya mostró el error
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((response) => this.showPage(response));
    this.reload$.next();
  }

  protected search(): void {
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }
    this.appliedFilter = this.filterForm.getRawValue();
    this.reload(true);
  }

  protected clear(): void {
    this.filterForm.reset();
    this.search();
  }

  protected openRegister(): void {
    this.dialog
      .open<RegisterBonusDialog, void, boolean>(RegisterBonusDialog, { width: '720px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notification.show('success', 'Bono registrado correctamente.');
        this.reload(true);
      });
  }

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.reload$.next();
  }

  protected isRegistered(bonus: BonusListItem): boolean {
    return bonus.bonusStateCode === BonusStates.Registered;
  }

  protected isPercentage(bonus: BonusListItem): boolean {
    return bonus.bonusTypeId === BonusTypes.SalaryPercentage;
  }

  protected cancel(bonus: BonusListItem): void {
    this.runConfirmedAction(
      {
        title: 'Anular bono',
        message: `¿Anular el bono de ${bonus.fullName} del periodo ${bonus.period}? Esta acción no se puede deshacer.`,
        confirmText: 'Anular',
      },
      () => this.bonusApi.cancel(bonus.bonusId),
      'Bono anulado.',
    );
  }

  protected remove(bonus: BonusListItem): void {
    this.runConfirmedAction(
      {
        title: 'Eliminar bono',
        message: `¿Eliminar el bono de ${bonus.fullName} del periodo ${bonus.period}? Dejará de aparecer en el listado.`,
        confirmText: 'Eliminar',
      },
      () => this.bonusApi.delete(bonus.bonusId),
      'Bono eliminado.',
    );
  }

  protected reload(resetPage: boolean): void {
    if (resetPage) {
      this.pageIndex.set(0);
    }
    this.reload$.next();
  }

  private runConfirmedAction(data: ConfirmDialogData, action: () => Observable<void>, successMessage: string): void {
    confirmAction(this.dialog, data)
      .pipe(filter(Boolean), switchMap(action))
      .subscribe({
        next: () => {
          this.notification.show('success', successMessage);
          this.reload(false);
        },
        error: noop, // el interceptor ya mostró el error
      });
  }

  /** Si la página quedó vacía (p. ej., se eliminó su último bono), retrocede una página. */
  private showPage(response: PagedResponse<BonusListItem>): void {
    if (response.data.length === 0 && this.pageIndex() > 0) {
      this.pageIndex.update((index) => index - 1);
      this.reload$.next();
      return;
    }
    this.page.set(response);
  }
}
