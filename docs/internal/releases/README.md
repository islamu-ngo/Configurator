# Configurator release inputs

Configurator owns its release inputs independently of Event. Native release
engineering creates governed YAML change fragments under `changes/` when a Configurator
release needs them. Event release history, signers and promotion authority are
not transfer inputs. Internal extraction increments carry their recorded
changelog disposition in Git; an empty YAML inventory is valid before the first
Configurator release.
