import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BonusFilter,
  BonusListItem,
  EmployeeLookup,
  RegisterBonusRequest,
  RegisteredBonus,
} from '../models/bonus';
import { PagedResponse } from '../models/paged-response';

@Injectable({ providedIn: 'root' })
export class BonusApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/Bonus`;

  search(filter: BonusFilter, pageIndex: number, pageSize: number): Observable<PagedResponse<BonusListItem>> {
    let params = new HttpParams().set('pageIndex', pageIndex).set('pageSize', pageSize);
    if (filter.departmentCode) params = params.set('departmentCode', filter.departmentCode);
    if (filter.employeeNumber !== null) params = params.set('employeeNumber', filter.employeeNumber);
    if (filter.period) params = params.set('period', filter.period);
    if (filter.bonusStateCode) params = params.set('bonusStateCode', filter.bonusStateCode);
    return this.http.get<PagedResponse<BonusListItem>>(this.baseUrl, { params });
  }

  getEmployee(departmentCode: string, employeeNumber: number): Observable<EmployeeLookup> {
    return this.http.get<EmployeeLookup>(
      `${this.baseUrl}/employee/${encodeURIComponent(departmentCode)}/${employeeNumber}`,
    );
  }

  register(request: RegisterBonusRequest): Observable<RegisteredBonus> {
    return this.http.post<RegisteredBonus>(this.baseUrl, request);
  }

  cancel(bonusId: number): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${bonusId}/cancel`, null);
  }

  delete(bonusId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${bonusId}`);
  }
}
