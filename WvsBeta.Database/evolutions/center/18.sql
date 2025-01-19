------- UP -------

UPDATE inventory_eqp SET iint = iint + 1 WHERE itemid = 1002145;
UPDATE inventory_eqp SET imdef = imdef - 3 WHERE itemid IN (1002363, 1002364, 1002365, 1002366);
UPDATE inventory_eqp SET iwatk = iwatk + 10 WHERE itemid = 1442002 ;

UPDATE storage_eqp SET iint = iint + 1 WHERE itemid = 1002145;
UPDATE storage_eqp SET imdef = imdef - 3 WHERE itemid IN (1002363, 1002364, 1002365, 1002366);
UPDATE storage_eqp SET iwatk = iwatk + 10 WHERE itemid = 1442002 ;


------- DOWN -------

UPDATE inventory_eqp SET iint = iint - 1 WHERE itemid = 1002145;
UPDATE inventory_eqp SET imdef = imdef + 3 WHERE itemid IN (1002363, 1002364, 1002365, 1002366);
UPDATE inventory_eqp SET iwatk = iwatk - 10 WHERE itemid = 1442002 ;

UPDATE storage_eqp SET iint = iint - 1 WHERE itemid = 1002145;
UPDATE storage_eqp SET imdef = imdef + 3 WHERE itemid IN (1002363, 1002364, 1002365, 1002366);
UPDATE storage_eqp SET iwatk = iwatk - 10 WHERE itemid = 1442002 ;