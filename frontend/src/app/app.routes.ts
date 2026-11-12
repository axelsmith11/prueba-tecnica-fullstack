import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'bonuses' },
  { path: 'bonuses', loadChildren: () => import('./features/bonuses/bonuses.routes').then((m) => m.bonusesRoutes) },
  { path: '**', redirectTo: 'bonuses' },
];
