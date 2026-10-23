import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { ConnectionFacade } from '../../../../../core/connection/application/connection.facade';
import { BrandMark } from '../../components/brand-mark/brand-mark';
import { StatusBadge } from '../../components/status-badge/status-badge';

/** The app frame: header with the name and the connection state, and the outlet for feature pages. */
@Component({
  selector: 'app-shell-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterOutlet, BrandMark, StatusBadge],
  templateUrl: './shell-page.html',
  styleUrl: './shell-page.scss',
})
export class ShellPage {
  private readonly connection = inject(ConnectionFacade);

  protected readonly status = this.connection.status;
}
