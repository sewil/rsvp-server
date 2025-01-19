-- Make ban_reason nullable


------- UP -------
ALTER TABLE `users`   
	CHANGE `ban_reason_message` `ban_reason_message` TEXT CHARSET latin1 COLLATE latin1_general_ci NULL;


------- DOWN -------
