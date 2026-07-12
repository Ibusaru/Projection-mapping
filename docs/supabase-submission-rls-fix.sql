-- Allow fish submissions even when an administrator is signed in on the same origin.
-- Run this once in Supabase SQL Editor for an existing deployment.

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
