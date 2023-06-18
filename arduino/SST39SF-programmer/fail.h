#ifndef SST39SF_PROGRAMMER_FAIL_H
#define SST39SF_PROGRAMMER_FAIL_H

#include <Arduino.h>

/**
 * @brief Goes into an infinite loop, sending a NAK message (see communication_util.h, sendNAKMessage) 
 * to serial at regular intervals.
 * 
 * @param errorMessage the message to send in the NAK message
 */
void fail(String errorMessage);

#endif  // SST39SF_PROGRAMMER_FAIL_H