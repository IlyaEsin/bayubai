import { ProfileView } from '@bayubai/ui';
import { createFileRoute, useNavigate } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/profile')({
  component: Profile,
});

function Profile() {
  const navigate = useNavigate();
  return <ProfileView onDeleted={() => void navigate({ to: '/sign-in', search: {} })} />;
}
