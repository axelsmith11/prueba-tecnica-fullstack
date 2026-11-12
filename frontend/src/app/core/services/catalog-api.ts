import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BonusReason, BonusType, Department, State } from '../models/catalog';

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/Catalog`;

  getDepartments(): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.baseUrl}/departments`);
  }

  getStates(masterStateCode: string): Observable<State[]> {
    return this.http.get<State[]>(`${this.baseUrl}/states/${masterStateCode}`);
  }

  getBonusTypes(): Observable<BonusType[]> {
    return this.http.get<BonusType[]>(`${this.baseUrl}/bonus-types`);
  }

  getBonusReasons(): Observable<BonusReason[]> {
    return this.http.get<BonusReason[]>(`${this.baseUrl}/bonus-reasons`);
  }
}
