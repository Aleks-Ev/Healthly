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

// null — поле не заполнено: показываем «Not provided», а не прочерк, чтобы не путать с «скрыто»
const NOT_PROVIDED = null;

export default function ProfileCard({ user, onEdit, onChangePassword }: ProfileCardProps) {
  const rows: [string, string | null][] = [
    ['Email', user.email],
    ['Phone', user.phoneNumber || NOT_PROVIDED],
    ['Date of birth', user.dateOfBirth ? formatDate(user.dateOfBirth) : NOT_PROVIDED],
    ['Testing city', user.city || NOT_PROVIDED],
    ['Height', user.height ? `${user.height} cm` : NOT_PROVIDED],
    ['Weight', user.weight ? `${user.weight} kg` : NOT_PROVIDED],
    ['Gender', user.gender || NOT_PROVIDED],
    ['Member since', formatDate(user.createdAt)],
  ];
  // По дате рождения и полу выбираются нормы анализов пациента — без них оценка менее точная
  const missesNormsData = user.role === 'Patient' && (!user.dateOfBirth || !user.gender);

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
      {user.patientCode && (
        <div className="mb-5 rounded-2xl bg-[#f3f6ef] px-4 py-3">
          <div className="text-xs text-[#6b756e]">Healthly ID</div>
          <div className="mt-0.5 font-mono text-lg font-semibold tracking-wider">{user.patientCode}</div>
          <div className="mt-1 text-xs text-[#6b756e]">Tell this ID at a partner laboratory so they can add your results.</div>
        </div>
      )}
      {user.laboratoryName && (
        <div className="mb-5 rounded-2xl bg-[#f3f6ef] px-4 py-3 text-sm">
          <div className="text-xs text-[#6b756e]">Laboratory</div>
          <div className="font-medium">{user.laboratoryName}</div>
        </div>
      )}
      <dl className="space-y-2.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="text-[#6b756e]">{label}</dt>
            <dd className={`text-right break-all ${value === NOT_PROVIDED ? 'text-[#9aa39b] italic' : 'font-medium'}`}>
              {value ?? 'Not provided'}
            </dd>
          </div>
        ))}
      </dl>
      {missesNormsData && (
        <p className="mt-4 rounded-2xl bg-amber-50 px-3 py-2 text-xs text-amber-800">
          Add the date of birth and gender{onEdit ? ' (Edit)' : ''}: they are used to choose the right reference ranges for lab results.
        </p>
      )}
      {onChangePassword && (
        <Button variant="secondary" size="sm" className="mt-5 w-full" onClick={onChangePassword}>
          <KeyRound className="w-3.5 h-3.5" />
          Change password
        </Button>
      )}
    </Card>
  );
}
