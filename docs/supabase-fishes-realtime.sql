-- Run this once in Supabase SQL Editor for an existing deployment.
-- It lets Unity and the admin page receive public.fishes changes over Supabase Realtime.

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
