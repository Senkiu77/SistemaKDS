ALTER TABLE pedidos DROP CONSTRAINT IF EXISTS pedidos_estado_check;
ALTER TABLE pedidos ADD CONSTRAINT pedidos_estado_check
    CHECK (estado IN ('Solicitado', 'Pendiente', 'Preparando', 'Listo', 'Entregado'));

DO $$
DECLARE
    roles TEXT;
    restriccion TEXT;
BEGIN
    FOR restriccion IN
        SELECT con.conname FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        WHERE rel.relname = 'usuarios' AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid) ILIKE '%rol%'
          AND pg_get_constraintdef(con.oid) NOT ILIKE '%''Admin''%'
    LOOP
        EXECUTE format('ALTER TABLE usuarios DROP CONSTRAINT %I', restriccion);

        SELECT string_agg(quote_literal(r), ', ') INTO roles
        FROM (SELECT DISTINCT rol AS r FROM usuarios
              UNION SELECT unnest(ARRAY['Mesero', 'Cocinero', 'Cocina', 'Admin'])) t;

        EXECUTE format('ALTER TABLE usuarios ADD CONSTRAINT %I CHECK (rol IN (%s))', restriccion, roles);
    END LOOP;
END $$;

INSERT INTO usuarios (nombre, rol)
SELECT 'Administrador', 'Admin'
WHERE NOT EXISTS (SELECT 1 FROM usuarios WHERE rol = 'Admin');

ALTER TABLE platos ADD COLUMN IF NOT EXISTS categoria VARCHAR(20);

UPDATE platos SET categoria = CASE
    WHEN nombre ~* '(limonada|bebida|gaseosa|jugo|agua|cerveza|café|cafe|malteada|soda|coca|cola|pepsi|sprite|postob|tinto|chocolate|avena|vino|milo|nectar)' THEN 'Bebida'
    WHEN nombre ~* '(papas|papitas|yuca|aros|ensalada|patac|nuggets|extra|adicional|queso|tocineta|salsa)' THEN 'Acompañamiento'
    ELSE 'Comida'
END
WHERE categoria IS NULL OR categoria NOT IN ('Comida', 'Bebida', 'Acompañamiento');

ALTER TABLE platos ALTER COLUMN categoria SET DEFAULT 'Comida';
ALTER TABLE platos ALTER COLUMN categoria SET NOT NULL;
ALTER TABLE platos DROP CONSTRAINT IF EXISTS platos_categoria_check;
ALTER TABLE platos ADD CONSTRAINT platos_categoria_check
    CHECK (categoria IN ('Comida', 'Bebida', 'Acompañamiento'));

ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS clave_hash VARCHAR(200);
