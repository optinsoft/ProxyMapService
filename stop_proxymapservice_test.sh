#!/bin/sh
lsof -ti:5001 | xargs kill
