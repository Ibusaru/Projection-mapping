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

create unique index if not exists fishes_nickname_unique_idx on public.fishes (nickname);

alter table public.fishes enable row level security;

drop policy if exists "Anyone can insert fishes" on public.fishes;
create policy "Anyone can insert fishes"
on public.fishes
for insert
to anon
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
to anon
using (true);

drop policy if exists "Anyone can update fishes" on public.fishes;
create policy "Anyone can update fishes"
on public.fishes
for update
to anon
using (true)
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
to anon
using (bucket_id = 'fish-drawings');

drop policy if exists "Anyone can upload fish drawings" on storage.objects;
create policy "Anyone can upload fish drawings"
on storage.objects
for insert
to anon
with check (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
);

drop policy if exists "Anyone can update fish drawings" on storage.objects;
create policy "Anyone can update fish drawings"
on storage.objects
for update
to anon
using (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
)
with check (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
);

drop policy if exists "Anyone can delete fish drawings" on storage.objects;
create policy "Anyone can delete fish drawings"
on storage.objects
for delete
to anon
using (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
);
