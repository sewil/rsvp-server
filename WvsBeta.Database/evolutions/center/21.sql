-- Update Purple Mystra to have 3 LUK stat, not INT


------- UP -------

UPDATE storage_eqp SET iint = 0, iluk = 3 WHERE iint = 3 AND itemid = 1082143;
UPDATE inventory_eqp SET iint = 0, iluk = 3 WHERE iint = 3 AND itemid = 1082143;

------- DOWN -------

