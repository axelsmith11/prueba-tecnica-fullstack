import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'bonuses' },
  { path: 'bonuses', loadComponent: () => import('./features/bonuses/bonuses').then((m) => m.Bonuses) },
  { path: '**', redirectTo: 'bonuses' },
];
