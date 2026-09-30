import { hasRole, loadSession, roles } from '@bayubai/ui';
import { createFileRoute, redirect } from '@tanstack/react-router';
import { CreateConsultantForm } from '../../../admin/CreateConsultantForm';

export const Route = createFileRoute('/_authed/admin/consultants')({
  // The API enforces the admin policy; this only keeps non-admins away from a form that would fail.
  beforeLoad: async ({ context }) => {
    const me = await loadSession(context.queryClient);
    if (!me || !hasRole(me, roles.admin)) {
      throw redirect({ to: '/' });
    }
  },
  component: CreateConsultantForm,
});
