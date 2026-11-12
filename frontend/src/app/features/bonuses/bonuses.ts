import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MasterStateCodes } from '../../core/models/catalog';
import { CatalogApi } from '../../core/services/catalog-api';

/**
 * Pantalla de bonos de desempeño.
 * Ya carga los catálogos como ejemplo de consumo de la API.
 * Pendiente: filtros, listado paginado desde el servidor, acciones y modal de registro.
 */
@Component({
  selector: 'app-bonuses',
  templateUrl: './bonuses.html',
  styleUrl: './bonuses.css',
})
export class Bonuses {
  private readonly catalogApi = inject(CatalogApi);

  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusStates = toSignal(this.catalogApi.getStates(MasterStateCodes.Bonus), { initialValue: [] });
}
