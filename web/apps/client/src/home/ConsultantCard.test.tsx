import { renderWithProviders } from '@bayubai/ui/testing';
import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ConsultantCard } from './ConsultantCard';

describe('ConsultantCard', () => {
  it("shows the consultant's local time in her own zone", () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-01-05T20:30:00Z'));

    renderWithProviders(
      <ConsultantCard consultant={{ userId: 'c1', displayName: 'Consultant', timeZone: 'Europe/Moscow', linkedAt: '2026-01-01T00:00:00Z' }} />,
      'ru',
    );

    expect(screen.getByText('Consultant')).toBeInTheDocument();
    expect(screen.getByText('Сейчас у консультанта 23:30 (Europe/Moscow)')).toBeInTheDocument();
  });
});
