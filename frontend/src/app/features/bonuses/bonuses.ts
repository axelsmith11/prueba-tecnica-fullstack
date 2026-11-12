import { BreakpointObserver } from '@angular/cdk/layout';
import { DecimalPipe, NgTemplateOutlet, formatNumber } from '@angular/common';
import { Component, Injector, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EMPTY, Observable, Subject, catchError, filter, finalize, map, noop, switchMap } from 'rxjs';
import { BonusFilter, BonusListItem, BonusSummary, EMPTY_BONUS_FILTER, MAX_EXPORT_ROWS } from '../../core/models/bonus';
import { BonusStates, BonusTypes, MasterStateCodes } from '../../core/models/catalog';
import { PagedResponse } from '../../core/models/paged-response';
import { BonusApi } from '../../core/services/bonus-api';
import { CatalogApi } from '../../core/services/catalog-api';
import { Notification } from '../../core/services/notification';
import { csvFileName, downloadFile } from '../../core/utils/download';
import { recentPeriods } from '../../core/utils/period';
import { ConfirmDialogData, confirmAction } from '../../shared/confirm-dialog/confirm-dialog';
import { BonusSummaryCards } from './bonus-summary-cards/bonus-summary-cards';
import { PaymentDetailDialog, PaymentDetailDialogData } from './payment-detail-dialog/payment-detail-dialog';
import { RegisterBonusDialog } from './register-bonus-dialog/register-bonus-dialog';

/** Pantalla de bonos de desempeño: filtros, listado paginado en servidor y acciones por fila. */
@Component({
  selector: 'app-bonuses',
  imports: [
    DecimalPipe,
    NgTemplateOutlet,
    ReactiveFormsModule,
    BonusSummaryCards,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
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
  // Los diálogos se abren con el MatDialog raíz: se les pasa este injector para que vean los proveedores de la ruta.
  private readonly injector = inject(Injector);
  private readonly notification = inject(Notification);
  private readonly reload$ = new Subject<void>();
  private readonly summaryReload$ = new Subject<void>();
  private appliedFilter: BonusFilter = EMPTY_BONUS_FILTER;

  protected readonly pageSize = 10;
  protected readonly displayedColumns = [
    'actions', 'state', 'period', 'employee', 'bonus', 'reason', 'gross', 'tax', 'net',
  ];
  /** En pantallas angostas la tabla se reemplaza por tarjetas para evitar el scroll horizontal. */
  protected readonly compact = toSignal(
    inject(BreakpointObserver).observe('(max-width: 1199.98px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusStates = toSignal(this.catalogApi.getStates(MasterStateCodes.Bonus), { initialValue: [] });
  protected readonly periodOptions = recentPeriods();
  protected readonly loading = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly page = signal<PagedResponse<BonusListItem> | null>(null);
  protected readonly summary = signal<BonusSummary | null>(null);
  protected readonly exporting = signal(false);

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
    this.summaryReload$
      .pipe(
        switchMap(() => this.bonusApi.summary(this.appliedFilter).pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(),
      )
      .subscribe((summary) => this.summary.set(summary));
    this.reload(false);
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
      .open<RegisterBonusDialog, void, boolean>(RegisterBonusDialog, { width: '720px', maxWidth: '95vw', injector: this.injector })
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

  /** Monto fijo ("150.00") o porcentaje ("5.00 %"), igual en la tabla y en las tarjetas. */
  protected bonusValue(bonus: BonusListItem): string {
    const value = formatNumber(bonus.bonusValue, 'en-US', '1.2-2');
    return bonus.bonusTypeId === BonusTypes.SalaryPercentage ? `${value} %` : value;
  }

  protected isPaid(bonus: BonusListItem): boolean {
    return bonus.bonusStateCode === BonusStates.Paid;
  }

  protected showPaymentDetail(bonus: BonusListItem): void {
    this.dialog.open<PaymentDetailDialog, PaymentDetailDialogData>(PaymentDetailDialog, {
      data: { bonusId: bonus.bonusId, fullName: bonus.fullName, period: bonus.period },
      width: '520px',
      maxWidth: '95vw',
      injector: this.injector,
    });
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

  protected pay(bonus: BonusListItem): void {
    const gross = formatNumber(bonus.grossAmount, 'en-US', '1.2-2');
    this.runConfirmedAction(
      {
        title: 'Pagar bono',
        message: `¿Pagar el bono de ${bonus.fullName} (${bonus.period})? El bruto de ${gross} se repartirá entre sus proyectos según las horas.`,
        confirmText: 'Pagar',
      },
      () => this.bonusApi.pay(bonus.bonusId),
      'Bono pagado y distribuido por proyecto.',
    );
  }

  protected exportCsv(): void {
    const total = this.page()?.totalRecords ?? 0;
    if (total === 0) {
      this.notification.show('warning', 'No hay bonos para exportar con estos filtros.');
      return;
    }
    if (total > MAX_EXPORT_ROWS) {
      this.notification.show('warning', 'Hay más de 10 000 bonos con estos filtros; acota la búsqueda para exportar.');
      return;
    }
    this.exporting.set(true);
    this.bonusApi
      .export(this.appliedFilter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({ next: (file) => downloadFile(file, csvFileName()), error: noop });
  }

  protected reload(resetPage: boolean): void {
    if (resetPage) {
      this.pageIndex.set(0);
    }
    this.reload$.next();
    this.summaryReload$.next();
  }

  private runConfirmedAction(data: ConfirmDialogData, action: () => Observable<void>, successMessage: string): void {
    confirmAction(this.dialog, data, this.injector)
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
