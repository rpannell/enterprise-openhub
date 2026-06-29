-- ─────────────────────────────────────────────────────────────────────────────
-- Temporal versioning trigger for catalog.products
--
-- On UPDATE: closes the old sys_period, archives the old row to products_history,
--            then opens a new period on the live row.
-- On DELETE: archives the row with a closed period before it is removed.
--
-- Uses BEFORE timing so RETURN NEW can modify sys_period before the write lands.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION catalog.versioning_trigger()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    v_now TIMESTAMPTZ := now();
BEGIN
    IF TG_OP = 'UPDATE' THEN
        -- Archive the previous version with a closed time range
        OLD.sys_period := tstzrange(lower(OLD.sys_period), v_now);
        INSERT INTO catalog.products_history VALUES (OLD.*);

        -- Open a fresh period and stamp updated_at on the new version
        NEW.sys_period := tstzrange(v_now, NULL);
        NEW.updated_at := v_now;
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        -- Archive then let the deletion proceed
        OLD.sys_period := tstzrange(lower(OLD.sys_period), v_now);
        INSERT INTO catalog.products_history VALUES (OLD.*);
        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$;

CREATE TRIGGER products_versioning
BEFORE UPDATE OR DELETE ON catalog.products
FOR EACH ROW EXECUTE FUNCTION catalog.versioning_trigger();
