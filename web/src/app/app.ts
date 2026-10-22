import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ShellPage } from './features/shell/ui/pages/shell-page/shell-page';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ShellPage],
  template: '<app-shell-page />',
})
export class App {}
