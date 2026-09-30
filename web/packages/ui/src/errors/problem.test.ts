import { ApiProblem } from '@bayubai/api-client';
import { createI18n } from '@bayubai/i18n';
import { describe, expect, it } from 'vitest';
import { invalidFields, problemMessage } from './problem';

const i18n = createI18n('en');

describe('problemMessage', () => {
  it('translates a known error code', () => {
    expect(problemMessage(i18n, new ApiProblem(410, 'identity.invite_expired'))).toBe(
      'The invitation has expired. Ask the consultant for a new one.',
    );
  });

  it('falls back to the status when the answer has no code', () => {
    expect(problemMessage(i18n, new ApiProblem(401, undefined))).toBe('Your session has ended. Please sign in again.');
  });

  it('falls back to the status for a code the dictionary does not know yet', () => {
    expect(problemMessage(i18n, new ApiProblem(403, 'billing.future_code'))).toBe('You do not have permission to do this');
  });

  it('reports network failures separately', () => {
    expect(problemMessage(i18n, new ApiProblem(0, undefined))).toBe('Cannot reach the server. Check your connection and try again.');
  });

  it('uses a generic message for anything else', () => {
    expect(problemMessage(i18n, new Error('boom'))).toBe('Something went wrong. Please try again.');
    expect(problemMessage(i18n, new ApiProblem(500, undefined))).toBe('Something went wrong. Please try again.');
  });
});

describe('invalidFields', () => {
  it('lists the fields the API rejected', () => {
    expect([...invalidFields(new ApiProblem(400, 'validation_failed', { email: ['invalid'] }))]).toEqual(['email']);
    expect(invalidFields(new Error('x')).size).toBe(0);
  });
});
