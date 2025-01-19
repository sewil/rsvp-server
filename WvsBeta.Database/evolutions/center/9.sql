------- UP -------

ALTER TABLE gamestats 
ADD COLUMN omokscore INT NOT NULL DEFAULT 2000 AFTER omoklosses,
ADD COLUMN matchcardscore INT NOT NULL DEFAULT 2000 AFTER matchcardlosses;

------- DOWN -------

ALTER TABLE gamestats
DROP COLUMN omokscore,
DROP COLUMN matchcardscore;