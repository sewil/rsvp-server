-- Make ban reason a text field rather than varchar

------- UP -------

ALTER TABLE `users`   
	CHANGE `ban_reason_message` `ban_reason_message` TEXT CHARSET latin1 COLLATE latin1_general_ci NOT NULL;

------- DOWN -------

-- Put your down queries here, such as DROP TABLE, etc.
