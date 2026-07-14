-- Run this after the existing fishes table has been created.
-- It keeps the newest 75 submitted fish and removes older database rows.
-- Storage objects are append-only in the current public policy; remove any
-- orphaned PNGs through the Storage API or an authenticated maintenance job.

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

-- Apply the limit immediately to rows that already exist.
delete from public.fishes
where id in (
  select id
  from public.fishes
  order by created_at desc, id desc
  offset 75
);
