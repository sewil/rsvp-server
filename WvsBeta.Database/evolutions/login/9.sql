------- UP -------

ALTER TABLE users 
DROP COLUMN gm,
DROP COLUMN `online`,
DROP COLUMN salt,
DROP COLUMN creation_date,
DROP COLUMN superadmin,
DROP COLUMN donator,
DROP COLUMN web_admin,
DROP COLUMN betakey,
DROP COLUMN affiliate;

ALTER TABLE users MODIFY gender tinyint(2) default 10;

------- DOWN -------

ALTER TABLE users MODIFY gender tinyint(1) NULL default NULL;
alter table users 
ADD COLUMN gm tinyint(4) default 0,
ADD COLUMN `online` int(5) default 0,
ADD COLUMN salt tinyint(4) default 0,
ADD COLUMN creation_date datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
ADD COLUMN superadmin tinyint(4) default 0,
ADD COLUMN donator tinyint(4) NULL default 0,
ADD COLUMN web_admin tinyint(4) default 0,
ADD COLUMN betakey varchar(255) NULL DEFAULT NULL,
ADD COLUMN affiliate int(11)  NULL default NULL;