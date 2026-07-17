create extension if not exists pgcrypto;

create table if not exists public.fishes (
  id uuid primary key default gen_random_uuid(),
  nickname text not null,
  species text not null,
  main_color text not null,
  sub_color text not null,
  pattern text not null default 'none',
  size text not null default 'medium',
  personality text not null default 'calm',
  texture_path text,
  texture_url text,
  spawned boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint fishes_nickname_length check (char_length(nickname) between 1 and 12),
  constraint fishes_species_check check (species in ('clownfish', 'jellyfish', 'tuna', 'original')),
  constraint fishes_pattern_check check (pattern in ('none', 'stripe', 'spot')),
  constraint fishes_size_check check (size in ('small', 'medium', 'large')),
  constraint fishes_personality_check check (personality in ('calm', 'fast', 'schooling')),
  constraint fishes_main_color_hex check (main_color ~ '^#[0-9A-Fa-f]{6}$'),
  constraint fishes_sub_color_hex check (sub_color ~ '^#[0-9A-Fa-f]{6}$'),
  constraint fishes_texture_path_png check (texture_path is null or texture_path like '%.png'),
  constraint fishes_texture_url_http check (texture_url is null or texture_url like 'http%')
);

alter table public.fishes
  add column if not exists texture_path text,
  add column if not exists texture_url text,
  add column if not exists updated_at timestamptz not null default now();

do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'fishes_texture_path_png'
  ) then
    alter table public.fishes
      add constraint fishes_texture_path_png check (texture_path is null or texture_path like '%.png') not valid;
  end if;
end;
$$;

do $$
begin
  if not exists (
    select 1 from pg_constraint where conname = 'fishes_texture_url_http'
  ) then
    alter table public.fishes
      add constraint fishes_texture_url_http check (texture_url is null or texture_url like 'http%') not valid;
  end if;
end;
$$;

do $$
declare
  constraint_name text;
begin
  for constraint_name in
    select c.conname
    from pg_constraint c
    join pg_class t on t.oid = c.conrelid
    join pg_namespace n on n.oid = t.relnamespace
    where n.nspname = 'public'
      and t.relname = 'fishes'
      and c.contype = 'u'
      and (
        select array_agg(a.attname::text order by a.attnum)
        from unnest(c.conkey) key(attnum)
        join pg_attribute a on a.attrelid = t.oid and a.attnum = key.attnum
      ) = array['nickname']
  loop
    execute format('alter table public.fishes drop constraint if exists %I', constraint_name);
  end loop;
end;
$$;

drop index if exists fishes_nickname_unique_idx;
drop index if exists fishes_nickname_key;

alter table public.fishes enable row level security;

drop policy if exists "Anyone can insert fishes" on public.fishes;
create policy "Anyone can insert fishes"
on public.fishes
for insert
to anon, authenticated
with check (
  char_length(nickname) between 1 and 12
  and species in ('clownfish', 'jellyfish', 'tuna', 'original')
  and pattern in ('none', 'stripe', 'spot')
  and size in ('small', 'medium', 'large')
  and personality in ('calm', 'fast', 'schooling')
  and main_color ~ '^#[0-9A-Fa-f]{6}$'
  and sub_color ~ '^#[0-9A-Fa-f]{6}$'
  and (texture_path is null or texture_path like '%.png')
  and (texture_url is null or texture_url like 'http%')
);

drop policy if exists "Anyone can read fishes" on public.fishes;
create policy "Anyone can read fishes"
on public.fishes
for select
to anon, authenticated
using (true);

drop policy if exists "Anyone can update fishes" on public.fishes;
-- Anonymous visitors can add new fish but cannot rewrite existing submissions.

create or replace function public.set_fishes_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists set_fishes_updated_at on public.fishes;
create trigger set_fishes_updated_at
before update on public.fishes
for each row
execute function public.set_fishes_updated_at();

create index if not exists fishes_created_at_idx on public.fishes (created_at desc);
create index if not exists fishes_spawned_created_at_idx on public.fishes (spawned, created_at);
create index if not exists fishes_updated_at_idx on public.fishes (updated_at desc);

-- Let Unity and the admin page receive INSERT/UPDATE/DELETE events over Supabase Realtime.
-- Existing deployments can run docs/supabase-fishes-realtime.sql once.
do $$
begin
  if not exists (
    select 1 from pg_publication where pubname = 'supabase_realtime'
  ) then
    execute 'create publication supabase_realtime';
  end if;

  if not exists (
    select 1
    from pg_publication_tables
    where pubname = 'supabase_realtime'
      and schemaname = 'public'
      and tablename = 'fishes'
  ) then
    execute 'alter publication supabase_realtime add table public.fishes';
  end if;
end;
$$;

-- Keep the active fish rotation bounded. Existing deployments can run
-- docs/supabase-fish-retention-75.sql to install this and trim old rows.
create or replace function public.trim_fishes_to_limit_75()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  delete from public.fishes
  where id in (
    select id
    from public.fishes
    order by created_at desc, id desc
    offset 75
  );
  return null;
end;
$$;

revoke all on function public.trim_fishes_to_limit_75() from public, anon, authenticated;

drop trigger if exists fishes_retention_limit_75 on public.fishes;
create trigger fishes_retention_limit_75
after insert on public.fishes
for each statement
execute function public.trim_fishes_to_limit_75();

delete from public.fishes
where id in (
  select id
  from public.fishes
  order by created_at desc, id desc
  offset 75
);

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('fish-drawings', 'fish-drawings', true, 2097152, array['image/png'])
on conflict (id) do update set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists "Anyone can read fish drawings" on storage.objects;
create policy "Anyone can read fish drawings"
on storage.objects
for select
to anon, authenticated
using (bucket_id = 'fish-drawings');

drop policy if exists "Anyone can upload fish drawings" on storage.objects;
create policy "Anyone can upload fish drawings"
on storage.objects
for insert
to anon, authenticated
with check (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
);

drop policy if exists "Anyone can update fish drawings" on storage.objects;
drop policy if exists "Anyone can delete fish drawings" on storage.objects;
-- Storage objects are append-only from the public app. Delete bad submissions from the Supabase dashboard.

-- 管理画面を使う場合は、このファイルに続けて supabase-admin-migration.sql を実行する。
