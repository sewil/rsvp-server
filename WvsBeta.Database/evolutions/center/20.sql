-- Update Purple Mystra to have 3 INT stat.


------- UP -------

UPDATE storage_eqp SET iint = 3 WHERE iint = 0 AND itemid = 1082143;
UPDATE inventory_eqp SET iint = 3 WHERE iint = 0 AND itemid = 1082143;

------- DOWN -------

