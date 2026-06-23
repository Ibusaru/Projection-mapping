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
  spawned boolean not null default false,
  created_at timestamptz not null default now(),
  constraint fishes_nickname_length check (char_length(nickname) between 1 and 12),
  constraint fishes_species_check check (species in ('clownfish', 'jellyfish', 'tuna', 'original')),
  constraint fishes_pattern_check check (pattern in ('none', 'stripe', 'spot')),
  constraint fishes_size_check check (size in ('small', 'medium', 'large')),
  constraint fishes_personality_check check (personality in ('calm', 'fast', 'schooling')),
  constraint fishes_main_color_hex check (main_color ~ '^#[0-9A-Fa-f]{6}$'),
  constraint fishes_sub_color_hex check (sub_color ~ '^#[0-9A-Fa-f]{6}$')
);

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
);

drop policy if exists "Anyone can read fishes" on public.fishes;
create policy "Anyone can read fishes"
on public.fishes
for select
to anon
using (true);

create index if not exists fishes_created_at_idx on public.fishes (created_at desc);
create index if not exists fishes_spawned_created_at_idx on public.fishes (spawned, created_at);
