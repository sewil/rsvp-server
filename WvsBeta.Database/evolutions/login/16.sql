-- Fix login tracking failing when unicode strings are saved. 


------- UP -------
ALTER TABLE `login_records`   
  CHANGE `windows_username` `windows_username` VARCHAR(255) CHARSET utf8mb4 NULL,
  CHANGE `windows_machine_name` `windows_machine_name` VARCHAR(255) CHARSET utf8mb4 NULL;


------- DOWN -------
