import { KeyRound, Pencil } from 'lucide-react';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { RoleBadge } from '@/components/ui/Badge';
import { formatDate } from '@/lib/format';
import type { User } from '@/types/api';

interface ProfileCardProps {
  user: User;
  onEdit?: () => void;
  // Только для своего профиля (дашборд): смена пароля требует текущий пароль
  onChangePassword?: () => void;
}

export default function ProfileCard({ user, onEdit, onChangePassword }: ProfileCardProps) {
  const rows: [string, string][] = [
    ['Email', user.email],
    ['Phone', user.phoneNumber || '—'],
    ['Date of birth', formatDate(user.dateOfBirth)],
    ['Testing city', user.city || '—'],
    ['Height', user.height ? `${user.height} cm` : '—'],
    ['Weight', user.weight ? `${user.weight} kg` : '—'],
    ['Gender', user.gender || '—'],
    ['Member since', formatDate(user.createdAt)],
  ];

  return (
    <Card
      title="Profile"
      actions={onEdit && (
        <Button variant="secondary" size="sm" onClick={onEdit}>
          <Pencil className="w-3.5 h-3.5" />
          Edit
        </Button>
      )}
    >
      <div className="flex items-center gap-3 mb-5">
        <div className="w-12 h-12 rounded-full bg-[#dce8d8] text-[#3a5235] flex items-center justify-center text-lg font-semibold">
          {user.fullName.charAt(0).toUpperCase() || '?'}
        </div>
        <div>
          <div className="font-semibold">{user.fullName}</div>
          <RoleBadge role={user.role} />
        </div>
      </div>
      <dl className="space-y-2.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="text-[#6b756e]">{label}</dt>
            <dd className="font-medium text-right break-all">{value}</dd>
          </div>
        ))}
      </dl>
      {onChangePassword && (
        <Button variant="secondary" size="sm" className="mt-5 w-full" onClick={onChangePassword}>
          <KeyRound className="w-3.5 h-3.5" />
          Change password
        </Button>
      )}
    </Card>
  );
}
