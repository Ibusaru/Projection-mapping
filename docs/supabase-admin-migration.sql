-- 管理画面を追加するときに、既存の docs/supabase.sql の実行後に一度だけ実行する。

create table if not exists public.admins (
  user_id uuid primary key references auth.users(id) on delete cascade,
  created_at timestamptz not null default now()
);

alter table public.admins enable row level security;

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.admins where user_id = auth.uid()
  );
$$;

revoke all on function public.is_admin() from public;
grant execute on function public.is_admin() to authenticated;

drop policy if exists "Admins can read fishes" on public.fishes;
create policy "Admins can read fishes"
on public.fishes
for select
to authenticated
using (public.is_admin());

drop policy if exists "Admins can delete fish drawings" on storage.objects;
create policy "Admins can delete fish drawings"
on storage.objects
for delete
to authenticated
using (bucket_id = 'fish-drawings' and public.is_admin());

create table if not exists public.admin_commands (
  id uuid primary key default gen_random_uuid(),
  action text not null,
  payload jsonb not null default '{}'::jsonb,
  created_by uuid references auth.users(id) on delete set null default auth.uid(),
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default (now() + interval '30 seconds'),
  constraint admin_commands_action_check check (
    action in ('camera_aerial', 'camera_roam', 'camera_focus', 'delete_fish')
  )
);

alter table public.admin_commands enable row level security;

drop policy if exists "Unity can read active admin commands" on public.admin_commands;
create policy "Unity can read active admin commands"
on public.admin_commands
for select
to anon
using (expires_at > now());

drop policy if exists "Admins can read admin commands" on public.admin_commands;
create policy "Admins can read admin commands"
on public.admin_commands
for select
to authenticated
using (public.is_admin());

drop policy if exists "Admins can issue admin commands" on public.admin_commands;
create policy "Admins can issue admin commands"
on public.admin_commands
for insert
to authenticated
with check (public.is_admin() and created_by = auth.uid());

create index if not exists admin_commands_created_at_idx
on public.admin_commands (created_at desc);

create or replace function public.admin_delete_fish(target_id uuid)
returns text
language plpgsql
security definer
set search_path = public
as $$
declare
  target_texture_path text;
begin
  if not public.is_admin() then
    raise exception 'administrator permission required' using errcode = '42501';
  end if;

  select texture_path into target_texture_path
  from public.fishes
  where id = target_id
  for update;

  if not found then
    raise exception 'fish not found' using errcode = 'P0002';
  end if;

  insert into public.admin_commands (action, payload, created_by)
  values ('delete_fish', jsonb_build_object('fish_id', target_id), auth.uid());

  delete from public.fishes where id = target_id;
  return target_texture_path;
end;
$$;

revoke all on function public.admin_delete_fish(uuid) from public;
grant execute on function public.admin_delete_fish(uuid) to authenticated;

-- 初回管理者の登録例:
-- 1. Supabase Dashboard > Authentication > Users で管理者ユーザーを作成する。
-- 2. そのUUIDを次の <USER_UUID> に置き換えて実行する。
-- insert into public.admins (user_id) values ('<USER_UUID>');
